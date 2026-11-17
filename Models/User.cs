using JQuery_ECommerceAss.Common.Enum;

namespace JQuery_ECommerceAss.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string? Username { get; set; }
        public string? UserPassword { get; set; }
        public string? UserGmail { get; set; }
        public int? LoginTry { get; set; }
        public string? UserPhoneNum { get; set; }
        public DateTime? DateTime { get; set; }
        public DateTime? RegisterDate { get; set; }
        public Status? UserStatus { get; set; }
    }
}
