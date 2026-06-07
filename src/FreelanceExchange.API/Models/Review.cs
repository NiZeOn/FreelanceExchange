using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreelanceExchange.API.Models;

public class Review
{
    public int Id { get; set; }
    public int Rating { get; set; }          // 1–5
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int AuthorId { get; set; }
    [ForeignKey("AuthorId")]
    public User Author { get; set; } = null!;

    public int RecipientId { get; set; }
    [ForeignKey("RecipientId")]
    public User Recipient { get; set; } = null!;

    public int OrderId { get; set; }
    [ForeignKey("OrderId")]
    public Order Order { get; set; } = null!;
}