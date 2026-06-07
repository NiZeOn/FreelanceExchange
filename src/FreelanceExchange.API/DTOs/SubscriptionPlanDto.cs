namespace FreelanceExchange.API.DTOs;
public class SubscriptionPlanDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Days { get; set; }
    public string TargetRole { get; set; } = "Freelancer";
}