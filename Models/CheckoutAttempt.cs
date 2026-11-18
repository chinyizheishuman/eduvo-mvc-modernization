namespace JQuery_ECommerceAss.Models;

public class CheckoutAttempt
{
    public int CheckoutAttemptID { get; set; }
    public required string RequestId { get; set; }
    public int UserID { get; set; }
    public DateTime DateTime { get; set; }
    public required string Status { get; set; }
}
