using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreelanceExchange.API.Models;

public class Response
{
    public int Id { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public decimal ProposedPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";

    public int FreelancerId { get; set; }
    [ForeignKey("FreelancerId")]
    public User Freelancer { get; set; } = null!;

    public int OrderId { get; set; }
    [ForeignKey("OrderId")]
    public Order Order { get; set; } = null!;
}