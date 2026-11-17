namespace JQuery_ECommerceAss.Models
{
    public class Cart
    {
        public int CartID { get; set; }
        public Product? Product { get; set; }
        public int? ProductID { get; set; }
        public User? User { get; set; }
        public int? UserID { get; set; }
        public decimal? Price { get; set; }
        public decimal? Quantity { get; set; }
    }
}
