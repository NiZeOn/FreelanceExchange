using System;

namespace FreelanceExchange.API.Models;

public class Complaint
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "New";    // New, Reviewed, Rejected
    
    public int ComplainantId { get; set; }
    public User Complainant { get; set; } = null!;
    
    public int AccusedId { get; set; }
    public User Accused { get; set; } = null!;
}