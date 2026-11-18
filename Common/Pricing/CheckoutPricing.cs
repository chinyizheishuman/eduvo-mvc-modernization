using JQuery_ECommerceAss.Common.Enum;
using JQuery_ECommerceAss.Models;

namespace JQuery_ECommerceAss.Common.Pricing;

public static class CheckoutPricing
{
    public static decimal CalculateSubtotal(IEnumerable<Cart> items)
    {
        return items.Sum(item => (item.Price ?? 0m) * (item.Quantity ?? 0m));
    }

    public static decimal CalculateGst(decimal subtotal)
    {
        return subtotal * 0.06m;
    }

    public static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static decimal CalculatePromotion(
        decimal subtotal,
        Promotion? promotion,
        bool alreadyUsed,
        DateTime now)
    {
        if (promotion == null ||
            alreadyUsed ||
            promotion.PromotionStatus != Status.Active ||
            (promotion.StartDate.HasValue && promotion.StartDate.Value > now) ||
            (promotion.EndDate.HasValue && promotion.EndDate.Value < now) ||
            (promotion.MinPurchaseAmount.HasValue && promotion.MinPurchaseAmount.Value > 0 &&
             subtotal < promotion.MinPurchaseAmount.Value))
        {
            return 0m;
        }

        var discount = subtotal * (promotion.DiscountPercentage / 100m);
        if (promotion.MaxDiscountAmount.HasValue &&
            promotion.MaxDiscountAmount.Value > 0 &&
            discount > promotion.MaxDiscountAmount.Value)
        {
            discount = promotion.MaxDiscountAmount.Value;
        }

        return discount;
    }
}
