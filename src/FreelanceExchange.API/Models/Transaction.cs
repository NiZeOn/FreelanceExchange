using System;

namespace FreelanceExchange.API.Models;

public class Transaction
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;   // Deposit, Reserve, Payout, Refund
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";    // Pending, Completed, Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Кто инициировал (пользователь)
    public int InitiatorId { get; set; }
    public User Initiator { get; set; } = null!;
    
    // Связь с заказом (для escrow)
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
}