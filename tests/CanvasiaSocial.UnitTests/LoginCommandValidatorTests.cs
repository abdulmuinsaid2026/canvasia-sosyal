using CanvasiaSocial.Application.Authentication;

namespace CanvasiaSocial.UnitTests;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WithValidInput_IsValid()
    {
        var result = await _validator.ValidateAsync(
            new LoginCommand("admin@example.com", "AnyPassword", false));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("not-an-email", "password")]
    [InlineData("admin@example.com", "")]
    public async Task ValidateAsync_WithInvalidInput_IsInvalid(string email, string password)
    {
        var result = await _validator.ValidateAsync(new LoginCommand(email, password, false));

        Assert.False(result.IsValid);
    }
}
