using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreelanceExchange.API.Models;

public class Wallet
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    public decimal Balance { get; set; } = 0m;

    public string? LastFourDigits { get; set; }

    public string? CardToken { get; set; }

    public DateTime? CardExpiry { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}