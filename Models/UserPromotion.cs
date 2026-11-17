namespace JQuery_ECommerceAss.Models
{
    public class UserPromotion
    {
        public int UserPromotionID { get; set; }
        public User? User { get; set; }
        public int? UserID { get; set; }
        public Promotion? Promotion { get; set; }
        public int? PromotionID { get; set; }
        public DateTime? AssignedDate { get; set; }
        public int? UsedCount { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedDate { get; set; }
    }
}
