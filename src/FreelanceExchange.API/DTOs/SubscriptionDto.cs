namespace FreelanceExchange.API.DTOs;
public class SubscriptionDto
{
    public string Type { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UserId { get; set; }
}