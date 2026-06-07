using System.ComponentModel.DataAnnotations;

namespace FreelanceExchange.API.Models;

public class Achievement
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Icon { get; set; } = "award"; // lucide icon name
    [MaxLength(50)]
    public string TriggerType { get; set; } = string.Empty; // EmailVerified, FirstOrder, FiveOrders, FirstReview...
    public int RequiredCount { get; set; } = 1;
}