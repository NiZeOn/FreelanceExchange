namespace FreelanceExchange.API.Models;

public class Dispute
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int InitiatorId { get; set; } // кто открыл диспут (заказчик или фрилансер)
    public string Reason { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public string? Resolution { get; set; } // Решение модератора
    public string Status { get; set; } = "Open"; // Open, Resolved, Rejected
    public int? ModeratorId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Order Order { get; set; } = null!;
    public User Initiator { get; set; } = null!;
    public User? Moderator { get; set; }
}