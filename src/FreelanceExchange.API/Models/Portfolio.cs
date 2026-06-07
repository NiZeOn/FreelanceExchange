using System.ComponentModel.DataAnnotations.Schema;

namespace FreelanceExchange.API.Models;

public class Portfolio
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int FreelancerId { get; set; }
    [ForeignKey("FreelancerId")]
    public User Freelancer { get; set; } = null!;
}