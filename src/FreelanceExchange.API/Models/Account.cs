using System.ComponentModel.DataAnnotations.Schema;

namespace FreelanceExchange.API.Models;

public class Account
{
    [ForeignKey("User")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public decimal Balance { get; set; } = 0m;
    public decimal Blocked { get; set; } = 0m;
}