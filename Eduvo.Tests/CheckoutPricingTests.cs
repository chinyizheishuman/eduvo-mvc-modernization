using JQuery_ECommerceAss.Common.Enum;
using JQuery_ECommerceAss.Common.Pricing;
using JQuery_ECommerceAss.Models;

namespace Eduvo.Tests;

public class CheckoutPricingTests
{
    [Fact]
    public void SubtotalUsesDatabasePricesAndQuantities()
    {
        var cart = new[]
        {
            new Cart { Price = 12.50m, Quantity = 2 },
            new Cart { Price = 4.25m, Quantity = 3 }
        };

        Assert.Equal(37.75m, CheckoutPricing.CalculateSubtotal(cart));
    }

    [Fact]
    public void GstIsSixPercent()
    {
        Assert.Equal(2.265m, CheckoutPricing.CalculateGst(37.75m));
    }

    [Fact]
    public void PromotionHonorsPercentageMinimumAndCap()
    {
        var promotion = new Promotion
        {
            PromotionStatus = Status.Active,
            DiscountPercentage = 20m,
            MinPurchaseAmount = 30m,
            MaxDiscountAmount = 5m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        Assert.Equal(5m, CheckoutPricing.CalculatePromotion(40m, promotion, false, DateTime.UtcNow));
        Assert.Equal(0m, CheckoutPricing.CalculatePromotion(20m, promotion, false, DateTime.UtcNow));
        Assert.Equal(0m, CheckoutPricing.CalculatePromotion(40m, promotion, true, DateTime.UtcNow));
    }

    [Fact]
    public void ExpiredPromotionIsRejected()
    {
        var now = new DateTime(2026, 11, 17);
        var promotion = new Promotion
        {
            PromotionStatus = Status.Active,
            DiscountPercentage = 10m,
            EndDate = now.AddDays(-1)
        };

        Assert.Equal(0m, CheckoutPricing.CalculatePromotion(100m, promotion, false, now));
    }
}
