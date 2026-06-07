using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreelanceExchange.API.Models;

public class AdminMessage
{
    public int Id { get; set; }
    
    public int UserId { get; set; } // отправитель (пользователь)
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
    
    public bool IsFromAdmin { get; set; } // true – сообщение от админа, false – от пользователя
    
    public string Message { get; set; } = string.Empty;
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    public bool IsRead { get; set; } = false; // прочитано ли админом (для сообщений пользователя)
    
    public int? ReplyToId { get; set; } // для цепочки (опционально)
}