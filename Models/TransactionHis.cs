using System.ComponentModel.DataAnnotations;

namespace JQuery_ECommerceAss.Models
{
    public class TransactionHis
    {
        public int TransactionHisID { get; set; }
        public DateTime? DateTime { get; set; }
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        public User? User { get; set; }
        public int? UserID { get; set; }
        public decimal? SubtotalAmount { get; set; }
        public decimal? GstAmount { get; set; }
        public decimal? Promotion { get; set; }

        [Required]
        public int? SellerID { get; set; }
        public virtual Seller? Seller { get; set; }
    }
}
