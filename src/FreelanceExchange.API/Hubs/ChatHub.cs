using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;

namespace FreelanceExchange.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _context;
    public ChatHub(AppDbContext context) => _context = context;

    public async Task JoinOrderGroup(int orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
    }

    public async Task SendMessage(int orderId, string message)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);
        var userName = user?.FullName ?? userId.ToString();

        var chatMessage = new ChatMessage
        {
            OrderId = orderId,
            SenderId = userId,
            Message = message,
            SentAt = DateTime.UtcNow
        };
        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        await Clients.Group($"order_{orderId}").SendAsync("ReceiveMessage", userId, userName, message, chatMessage.SentAt);
    }
}