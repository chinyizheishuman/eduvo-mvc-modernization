using JQuery_ECommerceAss.Common.Enum;
using System.ComponentModel.DataAnnotations.Schema;

namespace JQuery_ECommerceAss.Models
{
    public class Product
    {
        public int ProductID { get; set; }
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? Quantity { get; set; }
        public Seller? Seller { get; set; }
        public int? SellerID { get; set; }
        public Status? ProductStatus { get; set; }
        public string? ImagePath { get; set; }

        [NotMapped] // 不影响到database
        public IFormFile? Image { get; set; } // 用于上传图片
    }
}
