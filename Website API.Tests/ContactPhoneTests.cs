using System.ComponentModel.DataAnnotations;
using Website_API.DTO;
using Xunit;

namespace Website_API.Tests;

public sealed class ContactPhoneTests
{
    // VELVEN-029: owner/agent phone fields accepted any text, e.g. "abc".
    [Theory]
    [InlineData("555 12 34 56", true)]
    [InlineData("555123456", true)]
    [InlineData("+995 555-12-34-56", true)]
    [InlineData("abc", false)]
    [InlineData("123", false)]
    [InlineData("555 abc 123", false)]
    public void OwnerAndAgentPhonesAreValidated(string phone, bool valid)
    {
        var dto = new CreateApartmentDto
        {
            Title = "Test",
            Price = 1000,
            OwnerPhoneNumber = phone,
            AgentPhoneNumber = phone,
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        var phoneErrors = results.Where(r => r.MemberNames.Any(m => m.EndsWith("PhoneNumber"))).ToList();
        Assert.Equal(valid ? 0 : 2, phoneErrors.Count);
    }

    [Fact]
    public void EmptyPhonesAreLeftToRequiredChecks()
    {
        var dto = new UpdateApartmentDto { OwnerPhoneNumber = null, AgentPhoneNumber = "" };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        Assert.DoesNotContain(results, r => r.MemberNames.Any(m => m.EndsWith("PhoneNumber")));
    }
}
