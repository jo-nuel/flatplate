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
builder.Services.AddScoped<IAccountMessageSender, DeferredAccountMessageSender>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

builder.Build().Run();
