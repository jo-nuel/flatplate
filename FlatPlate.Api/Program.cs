using FlatPlate.Server.Authentication;
using FlatPlate.Server.Data;
using FlatPlate.Server.Messaging;
using FlatPlate.Server.Models;
using FlatPlate.Server.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("FlatPlate")
    ?? throw new InvalidOperationException(
        "Configure the FlatPlate database connection string before starting the API.");

builder.Services.AddDbContext<AuthenticationDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
        return;
    }

    if (!databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Unsupported database provider '{databaseProvider}'.");
    }

    options.UseSqlServer(
        connectionString,
        sqlServer => sqlServer.EnableRetryOnFailure());
});

builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton(TimeProvider.System);

var oneTimeCodeKey = builder.Configuration["Authentication:OneTimeCodeKey"]
    ?? throw new InvalidOperationException(
        "Configure Authentication:OneTimeCodeKey before starting the API.");
var jwtSigningKey = builder.Configuration["Authentication:JwtSigningKey"]
    ?? throw new InvalidOperationException(
        "Configure Authentication:JwtSigningKey before starting the API.");
var jwtIssuer = builder.Configuration["Authentication:JwtIssuer"] ?? "FlatPlate.Api";
var jwtAudience = builder.Configuration["Authentication:JwtAudience"] ?? "FlatPlate.App";

builder.Services.AddSingleton<IOneTimeCodeService>(
    new OneTimeCodeService(oneTimeCodeKey));
builder.Services.AddSingleton<IAccessTokenService>(services =>
    new JwtAccessTokenService(
        new AccessTokenSettings(
            jwtIssuer,
            jwtAudience,
            jwtSigningKey,
            TimeSpan.FromMinutes(15)),
        services.GetRequiredService<TimeProvider>()));

var emailProvider = builder.Configuration["Email:Provider"] ?? "Deferred";
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    var smtpSettings = new SmtpSettings(
        RequiredSetting(builder.Configuration, "Email:Smtp:Host"),
        RequiredPort(builder.Configuration, "Email:Smtp:Port"),
        RequiredSecurityMode(builder.Configuration, "Email:Smtp:SecurityMode"),
        RequiredSetting(builder.Configuration, "Email:Smtp:FromAddress"),
        builder.Configuration["Email:Smtp:FromName"] ?? "FlatPlate",
        builder.Configuration["Email:Smtp:Username"],
        builder.Configuration["Email:Smtp:Password"]);
    smtpSettings.Validate();
    builder.Services.AddSingleton(smtpSettings);
    builder.Services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
    var publicApiBaseAddress = ResolvePublicApiBaseAddress(builder.Configuration);
    builder.Services.AddSingleton<IAccountMessageSender>(services =>
        new AccountEmailMessageSender(
            services.GetRequiredService<IEmailTransport>(),
            publicApiBaseAddress));
}
else if (emailProvider.Equals("Deferred", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IAccountMessageSender, DeferredAccountMessageSender>();
}
else
{
    throw new InvalidOperationException(
        $"Unsupported email provider '{emailProvider}'. Use 'Smtp' or 'Deferred'.");
}

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

var host = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = host.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
    AuthenticationDatabaseInitializer.Initialize(context);
}

host.Run();

static string RequiredSetting(IConfiguration configuration, string key) =>
    configuration[key]
    ?? throw new InvalidOperationException($"Configure {key} before starting the API.");

static int RequiredPort(IConfiguration configuration, string key) =>
    int.TryParse(RequiredSetting(configuration, key), out var port)
        ? port
        : throw new InvalidOperationException($"{key} must be a valid port number.");

static SmtpSecurityMode RequiredSecurityMode(
    IConfiguration configuration,
    string key) =>
    Enum.TryParse<SmtpSecurityMode>(
        RequiredSetting(configuration, key),
        ignoreCase: true,
        out var securityMode)
        ? securityMode
        : throw new InvalidOperationException(
            $"{key} must be 'StartTls' or 'SslOnConnect'.");

static Uri ResolvePublicApiBaseAddress(IConfiguration configuration)
{
    var configuredAddress = configuration["Authentication:PublicApiBaseUrl"];
    if (!string.IsNullOrWhiteSpace(configuredAddress))
    {
        return new Uri(configuredAddress, UriKind.Absolute);
    }

    var azureHostname = configuration["WEBSITE_HOSTNAME"];
    return string.IsNullOrWhiteSpace(azureHostname)
        ? new Uri("http://localhost:7071/api/")
        : new Uri($"https://{azureHostname.TrimEnd('/')}/api/");
}
