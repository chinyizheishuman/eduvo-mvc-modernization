using JQuery_ECommerceAss.Common.Enum;

namespace JQuery_ECommerceAss.Models
{
    public class Promotion
    {
        public int PromotionID { get; set; }
        public string? PromotionCode { get; set; }
        public string? PromotionName { get; set; }
        public string? Description { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal? MinPurchaseAmount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public Status? PromotionStatus { get; set; }
    }
}
