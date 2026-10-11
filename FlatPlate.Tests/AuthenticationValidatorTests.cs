using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Core.Validation;

namespace FlatPlate.Tests;

/// <summary>
/// Checks the account input rules shared by the WPF client and authentication API.
/// </summary>
public sealed class AuthenticationValidatorTests
{
    /// <summary>
    /// Confirms that a complete registration request is accepted.
    /// </summary>
    [Test]
    public void ValidateRegister_ValidRequest_ReturnsNoErrors()
    {
        var request = new RegisterRequest(
            "Jonathan",
            "jonathan@example.com",
            "FlatPlate9A",
            "FlatPlate9A");

        var result = AuthenticationValidator.Validate(request);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Confirms that registration reports every invalid field in one response.
    /// </summary>
    [Test]
    public void ValidateRegister_InvalidFields_ReturnsAllErrors()
    {
        var request = new RegisterRequest(
            " ",
            "not-an-email",
            "short",
            "different");

        var result = AuthenticationValidator.Validate(request);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(4));
            Assert.That(result.Select(error => error.Field), Does.Contain("DisplayName"));
            Assert.That(result.Select(error => error.Field), Does.Contain("Email"));
            Assert.That(result.Select(error => error.Field), Does.Contain("Password"));
            Assert.That(result.Select(error => error.Field), Does.Contain("ConfirmPassword"));
        });
    }

    /// <summary>
    /// Confirms that a long password still needs the required character groups.
    /// </summary>
    [TestCase("alllowercase9")]
    [TestCase("ALLUPPERCASE9")]
    [TestCase("NoNumbersHere")]
    public void ValidateRegister_MissingCharacterGroup_RejectsPassword(string password)
    {
        var request = new RegisterRequest(
            "Jonathan",
            "jonathan@example.com",
            password,
            password);

        var result = AuthenticationValidator.Validate(request);

        Assert.That(result.Select(error => error.Field), Does.Contain("Password"));
    }

    /// <summary>
    /// Confirms that surrounding spaces do not invalidate an otherwise valid email.
    /// </summary>
    [Test]
    public void ValidateSignIn_EmailWithSurroundingSpaces_ReturnsNoErrors()
    {
        var request = new SignInRequest("  jonathan@example.com  ", "password");

        var result = AuthenticationValidator.Validate(request);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Confirms that sign-in requires both a valid email and a password.
    /// </summary>
    [Test]
    public void ValidateSignIn_MissingCredentials_ReturnsTwoErrors()
    {
        var result = AuthenticationValidator.Validate(new SignInRequest("", ""));

        Assert.That(result, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Confirms that email verification requires a non-empty one-time token.
    /// </summary>
    [Test]
    public void ValidateEmailVerification_MissingToken_ReturnsTokenError()
    {
        var request = new VerifyEmailRequest("jonathan@example.com", " ");

        var result = AuthenticationValidator.Validate(request);

        Assert.That(result.Single().Field, Is.EqualTo("Token"));
    }

    /// <summary>
    /// Confirms that requesting a password reset only requires a valid email.
    /// </summary>
    [Test]
    public void ValidatePasswordResetRequest_ValidEmail_ReturnsNoErrors()
    {
        var request = new RequestPasswordResetRequest("jonathan@example.com");

        var result = AuthenticationValidator.Validate(request);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Confirms that a valid token and matching strong password can reset an account.
    /// </summary>
    [Test]
    public void ValidatePasswordReset_ValidRequest_ReturnsNoErrors()
    {
        var request = new ResetPasswordRequest(
            "jonathan@example.com",
            "123456",
            "NewFlatPlate9",
            "NewFlatPlate9");

        var result = AuthenticationValidator.Validate(request);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Confirms that password reset rejects an absent token and mismatched password.
    /// </summary>
    [Test]
    public void ValidatePasswordReset_InvalidRequest_ReturnsRelevantErrors()
    {
        var request = new ResetPasswordRequest(
            "jonathan@example.com",
            "",
            "NewFlatPlate9",
            "different");

        var result = AuthenticationValidator.Validate(request);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(error => error.Field), Does.Contain("Token"));
            Assert.That(
                result.Select(error => error.Field),
                Does.Contain("ConfirmNewPassword"));
        });
    }
}
