using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FreelanceExchange.API.Models;

[Index(nameof(UserId), nameof(CreatedAt))]
public class Notification
{
    public int Id { get; set; }
    
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    public string Text { get; set; } = string.Empty;
    
    public bool IsRead { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public string? Link { get; set; }
    
    public int UserId { get; set; }
    
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}