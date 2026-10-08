using Website_API.Controllers;
using Xunit;

namespace Website_API.Tests;

public sealed class AgentContactTests
{
    // VELVEN-018: the agent directory said "Not provided" while the profile showed the
    // phone from the agent's listings. Both now use the same contact phone.
    [Theory]
    [InlineData("599 11 22 33", "558 45 58 32", "599 11 22 33")]
    [InlineData(null, "558455832", "558455832")]
    [InlineData("  ", " 558455832 ", "558455832")]
    [InlineData(null, null, null)]
    public void ContactPhonePrefersProfileThenListing(string? profile, string? listing, string? expected)
    {
        Assert.Equal(expected, AgentsController.ContactPhone(profile, listing));
    }
}
