using System;

namespace FreelanceExchange.API.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Days { get; set; }
    public string TargetRole { get; set; } = "Freelancer";
}