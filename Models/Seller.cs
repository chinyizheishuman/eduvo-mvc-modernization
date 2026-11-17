using JQuery_ECommerceAss.Common.Enum;

namespace JQuery_ECommerceAss.Models
{
    public class Seller
    {
        public int SellerID { get; set; }
        public string? SellerUsername { get; set; }
        public string? SellerPassword { get; set; }
        public DateTime? DateTime { get; set; }
        public string? SellerEmail { get; set; }
        public int? LoginTry { get; set; }
        public Status? SellerStatus { get; set; }
        //允许seller access 他们的products
        public virtual ICollection<Product>? Products { get; set; }
    }
}
