using System.ComponentModel.DataAnnotations;
using Website_API.DTO;
using Xunit;

namespace Website_API.Tests;

public class RegistrationAccountTypeTests
{
    [Theory]
    [InlineData("Client", true)]
    [InlineData("Owner", true)]
    [InlineData("Admin", false)]
    [InlineData("Agent", false)]
    [InlineData("Manager", false)]
    [InlineData("", false)]
    public void PublicRegistrationAllowsOnlyClientOrOwner(string type, bool valid)
    {
        var dto = new RegisterDto { AccountType = type };
        var errors = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true));
    }

    [Fact]
    public void ExistingRegistrationRequestsDefaultToClient() => Assert.Equal("Client", new RegisterDto().AccountType);
}
