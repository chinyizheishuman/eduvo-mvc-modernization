namespace JQuery_ECommerceAss.Models
{
    public class Admin
    {
        public int AdminID { get; set; }
        public string AdminUsername { get; set; } = null!;
        public string AdminPassword { get; set; } = null!;
        public DateTime? DateTime { get; set; }
    }
}
