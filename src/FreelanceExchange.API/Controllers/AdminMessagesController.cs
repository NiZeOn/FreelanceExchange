using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;

namespace FreelanceExchange.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminMessagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminMessagesController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    
    private bool IsAdmin() =>
        User.IsInRole("Admin") || User.IsInRole("Moderator");

    // ========== ПОЛЬЗОВАТЕЛЬСКИЕ МЕТОДЫ ==========
    
    // GET: api/AdminMessages/my
    [HttpGet("my")]
    public async Task<IActionResult> GetMyMessages()
    {
        var userId = GetCurrentUserId();
        var messages = await _context.AdminMessages
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                m.Id,
                m.IsFromAdmin,
                m.Message,
                m.SentAt,
                m.IsRead
            })
            .ToListAsync();
        return Ok(messages);
    }

    // POST: api/AdminMessages/send
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendAdminMessageDto dto)
    {
        var userId = GetCurrentUserId();
        var message = new AdminMessage
        {
            UserId = userId,
            IsFromAdmin = false,
            Message = dto.Message,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.AdminMessages.Add(message);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Сообщение отправлено администратору" });
    }

    // ========== АДМИНИСТРАТИВНЫЕ МЕТОДЫ (только для Admin/Moderator) ==========
    
    // GET: api/AdminMessages/conversations
    [HttpGet("conversations")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetConversations()
    {
        // Получаем всех пользователей, которые писали админу
        var conversations = await _context.AdminMessages
            .GroupBy(m => m.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                UserName = _context.Users.Where(u => u.Id == g.Key).Select(u => u.FullName).FirstOrDefault(),
                LastMessage = g.OrderByDescending(m => m.SentAt).First().Message,
                LastMessageDate = g.Max(m => m.SentAt),
                UnreadCount = g.Count(m => !m.IsRead && !m.IsFromAdmin)
            })
            .OrderByDescending(c => c.LastMessageDate)
            .ToListAsync();
        return Ok(conversations);
    }

    // GET: api/AdminMessages/conversation/{userId}
    [HttpGet("conversation/{userId}")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetConversationWithUser(int userId)
    {
        var messages = await _context.AdminMessages
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                m.Id,
                m.IsFromAdmin,
                m.Message,
                m.SentAt,
                m.IsRead
            })
            .ToListAsync();
        
        // Помечаем все непрочитанные сообщения от пользователя как прочитанные
        var unread = await _context.AdminMessages
            .Where(m => m.UserId == userId && !m.IsRead && !m.IsFromAdmin)
            .ToListAsync();
        foreach (var m in unread)
            m.IsRead = true;
        await _context.SaveChangesAsync();
        
        return Ok(messages);
    }

    // POST: api/AdminMessages/reply/{userId}
    [HttpPost("reply/{userId}")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> ReplyToUser(int userId, [FromBody] SendAdminMessageDto dto)
    {
        var adminId = GetCurrentUserId();
        var admin = await _context.Users.FindAsync(adminId);
        if (admin == null) return Unauthorized();
        
        var message = new AdminMessage
        {
            UserId = userId,
            IsFromAdmin = true,
            Message = dto.Message,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.AdminMessages.Add(message);
        await _context.SaveChangesAsync();
        
        // Здесь можно добавить отправку уведомления пользователю через SignalR или Notification
        // Например, создать Notification:
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = "Новое сообщение от администратора",
            Text = $"Администратор ответил: {dto.Message.Substring(0, Math.Min(50, dto.Message.Length))}...",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        await _context.SaveChangesAsync();
        
        return Ok(new { Message = "Ответ отправлен" });
    }
}

// DTO
public class SendAdminMessageDto
{
    public string Message { get; set; } = string.Empty;
}