using System.Text.Json;
using Website_API.DTO;
using Xunit;

namespace Website_API.Tests;

public sealed class HomeMatchRequestTests
{
    // VELVEN-003: the Buy questionnaire skips the pet question and sends "hasPet": null.
    // That used to fail model binding (HTTP 400), so every Buy profile ended in
    // "We could not calculate your matches".
    [Fact]
    public void BuyProfileWithNullHasPetDeserializes()
    {
        const string json = """
            {"propertyGoal":"Buy","districts":["Vake"],"budgetMin":0,"budgetMax":2000000,
             "currency":"USD","adults":1,"bedrooms":3,"hasPet":null,"lifestyles":["QuietLifestyle"]}
            """;

        var request = JsonSerializer.Deserialize<HomeMatchProfileRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal("Buy", request!.PropertyGoal);
        Assert.Null(request.HasPet);
    }
}

public sealed class ViewingStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    // VELVEN-027: customers see a status for every viewing they requested.
    [Theory]
    [InlineData(Website_API.Models.CrmLeadStatus.New, false, 2, "Pending")]
    [InlineData(Website_API.Models.CrmLeadStatus.New, false, -2, "Past")]
    [InlineData(Website_API.Models.CrmLeadStatus.Viewing, true, -2, "Visited")]
    [InlineData(Website_API.Models.CrmLeadStatus.Won, true, -2, "DealDone")]
    [InlineData(Website_API.Models.CrmLeadStatus.Lost, false, 2, "Cancelled")]
    public void MapsLeadToCustomerStatus(Website_API.Models.CrmLeadStatus status, bool completed, int hoursFromNow, string expected)
    {
        Assert.Equal(
            expected,
            Website_API.Controllers.CrmController.ViewingStatus(status, completed, Now.AddHours(hoursFromNow), Now));
    }
}
