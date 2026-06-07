using System;

namespace FreelanceExchange.API.Models;

public class Subscription
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty; // FreelancerPro, CustomerBusiness
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Days { get; set; }
    public string Features { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    public bool IsActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
}