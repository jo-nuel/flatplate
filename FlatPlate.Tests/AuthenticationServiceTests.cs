using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Server.Authentication;
using FlatPlate.Server.Data;
using FlatPlate.Server.Messaging;
using FlatPlate.Server.Models;
using FlatPlate.Server.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Tests;

public sealed class AuthenticationServiceTests
{
    private const string Password = "FlatPlate123";

    [Test]
    public async Task RegisterVerifyAndSignIn_CompletesAccountLifecycle()
    {
        await using var environment = await AuthenticationTestEnvironment.CreateAsync();

        var registration = await environment.Service.RegisterAsync(new(
            "Jonathan",
            "jonathan@example.com",
            Password,
            Password));

        Assert.That(registration.IsSuccess, Is.True);
        Assert.That(environment.Messages.VerificationCode, Has.Length.EqualTo(6));

        var beforeVerification = await environment.Service.SignInAsync(new(
            "jonathan@example.com",
            Password));
        Assert.That(
            beforeVerification.Failure,
            Is.EqualTo(AuthenticationFailure.EmailNotVerified));

        var verification = await environment.Service.VerifyEmailAsync(new(
            "JONATHAN@example.com",
            environment.Messages.VerificationCode!));
        var signIn = await environment.Service.SignInAsync(new(
            "jonathan@example.com",
            Password));

        Assert.Multiple(() =>
        {
            Assert.That(verification.IsSuccess, Is.True);
            Assert.That(signIn.IsSuccess, Is.True);
            Assert.That(signIn.Value!.AccessToken, Is.Not.Empty);
            Assert.That(signIn.Value.User.DisplayName, Is.EqualTo("Jonathan"));
            Assert.That(signIn.Value.ExpiresAtUtc, Is.EqualTo(
                environment.Clock.GetUtcNow().AddMinutes(15)));
        });
    }

    [Test]
    public async Task PasswordReset_ChangesPasswordAndConsumesCode()
    {
        await using var environment = await AuthenticationTestEnvironment.CreateAsync();
        await environment.RegisterAndVerifyAsync();

        var request = await environment.Service.RequestPasswordResetAsync(new(
            "jonathan@example.com"));
        var reset = await environment.Service.ResetPasswordAsync(new(
            "jonathan@example.com",
            environment.Messages.PasswordResetCode!,
            "Replacement456",
            "Replacement456"));
        var repeatedReset = await environment.Service.ResetPasswordAsync(new(
            "jonathan@example.com",
            environment.Messages.PasswordResetCode!,
            "AnotherPassword789",
            "AnotherPassword789"));
        var oldSignIn = await environment.Service.SignInAsync(new(
            "jonathan@example.com",
            Password));
        var newSignIn = await environment.Service.SignInAsync(new(
            "jonathan@example.com",
            "Replacement456"));

        Assert.Multiple(() =>
        {
            Assert.That(request.IsSuccess, Is.True);
            Assert.That(reset.IsSuccess, Is.True);
            Assert.That(
                repeatedReset.Failure,
                Is.EqualTo(AuthenticationFailure.InvalidOrExpiredToken));
            Assert.That(
                oldSignIn.Failure,
                Is.EqualTo(AuthenticationFailure.InvalidCredentials));
            Assert.That(newSignIn.IsSuccess, Is.True);
        });
    }

    [Test]
    public async Task RequestPasswordReset_UsesSameResponseForUnknownEmail()
    {
        await using var environment = await AuthenticationTestEnvironment.CreateAsync();

        var knownShape = await environment.Service.RequestPasswordResetAsync(new(
            "missing@example.com"));

        Assert.Multiple(() =>
        {
            Assert.That(knownShape.IsSuccess, Is.True);
            Assert.That(
                knownShape.Value!.Message,
                Does.StartWith("If an account uses that email"));
            Assert.That(environment.Messages.PasswordResetCode, Is.Null);
        });
    }

    [Test]
    public async Task VerifyEmail_RejectsExpiredCode()
    {
        await using var environment = await AuthenticationTestEnvironment.CreateAsync();
        await environment.Service.RegisterAsync(new(
            "Jonathan",
            "jonathan@example.com",
            Password,
            Password));
        environment.Clock.Advance(TimeSpan.FromMinutes(31));

        var result = await environment.Service.VerifyEmailAsync(new(
            "jonathan@example.com",
            environment.Messages.VerificationCode!));

        Assert.That(
            result.Failure,
            Is.EqualTo(AuthenticationFailure.InvalidOrExpiredToken));
    }

    [Test]
    public void OneTimeCode_CannotBeReusedForAnotherPurpose()
    {
        var codes = new OneTimeCodeService(
            "test-only-code-signing-key-with-more-than-32-bytes");
        var hash = codes.Protect(
            "JONATHAN@EXAMPLE.COM",
            "123456",
            OneTimeCodePurpose.EmailVerification);

        Assert.Multiple(() =>
        {
            Assert.That(codes.Verify(
                "JONATHAN@EXAMPLE.COM",
                "123456",
                OneTimeCodePurpose.EmailVerification,
                hash), Is.True);
            Assert.That(codes.Verify(
                "JONATHAN@EXAMPLE.COM",
                "123456",
                OneTimeCodePurpose.PasswordReset,
                hash), Is.False);
        });
    }

    private sealed class AuthenticationTestEnvironment : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AuthenticationTestEnvironment(
            SqliteConnection connection,
            AuthenticationDbContext dbContext,
            AuthenticationService service,
            CapturingMessageSender messages,
            TestTimeProvider clock)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
            Messages = messages;
            Clock = clock;
        }

        public AuthenticationDbContext DbContext { get; }

        public AuthenticationService Service { get; }

        public CapturingMessageSender Messages { get; }

        public TestTimeProvider Clock { get; }

        public static async Task<AuthenticationTestEnvironment> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AuthenticationDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AuthenticationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var clock = new TestTimeProvider(
                new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero));
            var messages = new CapturingMessageSender();
            var passwordService = new PasswordService(
                new PasswordHasher<UserAccount>());
            var service = new AuthenticationService(
                dbContext,
                passwordService,
                new OneTimeCodeService(
                    "test-only-code-signing-key-with-more-than-32-bytes"),
                new JwtAccessTokenService(
                    new AccessTokenSettings(
                        "FlatPlate.Tests",
                        "FlatPlate.Tests.Client",
                        "test-only-jwt-signing-key-with-more-than-32-bytes",
                        TimeSpan.FromMinutes(15)),
                    clock),
                messages,
                clock);

            return new(connection, dbContext, service, messages, clock);
        }

        public async Task RegisterAndVerifyAsync()
        {
            await Service.RegisterAsync(new(
                "Jonathan",
                "jonathan@example.com",
                Password,
                Password));
            await Service.VerifyEmailAsync(new(
                "jonathan@example.com",
                Messages.VerificationCode!));
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class CapturingMessageSender : IAccountMessageSender
    {
        public string? VerificationCode { get; private set; }

        public string? PasswordResetCode { get; private set; }

        public Task SendEmailVerificationAsync(
            UserAccount account,
            string code,
            CancellationToken cancellationToken = default)
        {
            VerificationCode = code;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(
            UserAccount account,
            string code,
            CancellationToken cancellationToken = default)
        {
            PasswordResetCode = code;
            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
