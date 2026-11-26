using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class ListingAvailabilityTests
{
    private static readonly DateTime Today = new(2026, 11, 26);

    private static Apartment Home(string description, DateTime? field = null) =>
        new() { Description = description, AvailableFrom = field };

    [Fact]
    public void DateFieldWins()
    {
        Assert.Equal(new DateTime(2026, 12, 5), ListingAvailability.From(Home("ბინა თავისუფლდება იანვარში.", new DateTime(2026, 12, 5)), Today));
    }

    [Fact]
    public void MonthOnlyInDescriptionMeansFirstOfThatMonth()
    {
        Assert.Equal(new DateTime(2027, 1, 1), ListingAvailability.From(Home("ბინა თავისუფლდება იანვარში."), Today));
        Assert.Equal(new DateTime(2027, 3, 1), ListingAvailability.From(Home("Available from March."), Today));
        Assert.Equal(new DateTime(2027, 2, 1), ListingAvailability.From(Home("Квартира освобождается в феврале."), Today));
    }

    [Fact]
    public void WrittenDayAndMonthAndTag()
    {
        Assert.Equal(new DateTime(2026, 12, 18), ListingAvailability.From(Home("Free from 18 December"), Today));
        Assert.Equal(new DateTime(2026, 12, 18), ListingAvailability.From(Home("18 დეკემბრიდან"), Today));
        Assert.Equal(new DateTime(2027, 1, 10), ListingAvailability.From(Home("Deal: For Rent | Available from: 2027-01-10"), Today));
    }

    [Fact]
    public void NothingWrittenOrThisMonthMeansNow()
    {
        Assert.Null(ListingAvailability.From(Home("Pets allowed | Parking type: Garage"), Today));
        Assert.Null(ListingAvailability.From(Home("ბინა თავისუფლდება ნოემბერში."), Today));
    }
}
