namespace FreelanceExchange.API.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SenderId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}