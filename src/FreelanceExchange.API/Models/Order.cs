namespace FreelanceExchange.API.Models;

public class Order
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CustomerId { get; set; }
    public User Customer { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int? FreelancerId { get; set; }
    public User? Freelancer { get; set; }

    public string? FreelancerFileUrl { get; set; }
    public string? CustomerFileUrl { get; set; }
}