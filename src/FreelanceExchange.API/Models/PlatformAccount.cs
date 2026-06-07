namespace FreelanceExchange.API.Models;

public class PlatformAccount
{
    public int Id { get; set; }
    public decimal Balance { get; set; } = 0m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}