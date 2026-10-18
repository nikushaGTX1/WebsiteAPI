using Website_API.Controllers;
using Website_API.Models;
using Xunit;

namespace Website_API.Tests;

public class DealAmountTests
{
    [Fact]
    public void RentalEarnsOneMonthAndSaleThreePercent()
    {
        var rent = new Apartment { Price = 1300, Description = "Nice\n\nListing plan: Basic | Deal: For Rent | Currency: $" };
        var sale = new Apartment { Price = 250000, Description = "x\n\nDeal: For Sale | Currency: $" };
        Assert.Equal(1300m, CrmController.EstimateCommissionUsd(rent));
        Assert.Equal(7500m, CrmController.EstimateCommissionUsd(sale));
    }

    [Fact]
    public void NoEstimateWithoutAUsdPriceOrListing()
    {
        Assert.Null(CrmController.EstimateCommissionUsd(null));
        Assert.Null(CrmController.EstimateCommissionUsd(new Apartment { Price = 0, Description = "Deal: For Rent" }));
        Assert.Null(CrmController.EstimateCommissionUsd(new Apartment { Price = 3000, Description = "Deal: For Rent | Currency: ₾" }));
        // Older listings without a currency tag are priced in dollars.
        Assert.Equal(900m, CrmController.EstimateCommissionUsd(new Apartment { Price = 900, Description = "Deal: For Rent" }));
    }
}
