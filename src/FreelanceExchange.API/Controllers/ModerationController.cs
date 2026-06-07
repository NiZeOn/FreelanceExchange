using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;
using FreelanceExchange.API.DTOs;

namespace FreelanceExchange.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Moderator,Admin")]
public class ModerationController : ControllerBase
{
    private readonly AppDbContext _context;

    public ModerationController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET: api/Moderation/orders/pending
    [HttpGet("orders/pending")]
    public async Task<IActionResult> GetPendingOrders()
    {
        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Freelancer)
            .Where(o => o.Status == "PendingModeration")
            .Select(o => new
            {
                o.Id,
                o.Title,
                o.Description,
                o.Budget,
                o.Deadline,
                o.Status,
                o.CreatedAt,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.FullName,
                FreelancerId = o.FreelancerId,
                FreelancerName = o.Freelancer != null ? o.Freelancer.FullName : null,
                o.CustomerFileUrl,
                o.FreelancerFileUrl
            })
            .ToListAsync();

        return Ok(orders);
    }

    // POST: api/Moderation/orders/{id}/approve
    [HttpPost("orders/{id}/approve")]
    public async Task<IActionResult> ApproveOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Freelancer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Заказ не найден");

        if (order.Status != "PendingModeration")
            return BadRequest("Заказ не находится на модерации");

        // Средства уже заблокированы при создании заказа, ничего дополнительно не делаем
        // Просто меняем статус на "В работе"
        order.Status = "InProgress";

        // Уведомляем фрилансера, что заказ одобрен
        _context.Notifications.Add(new Notification
        {
            UserId = order.FreelancerId!.Value,
            Title = "Заказ одобрен модератором",
            Text = $"Ваш заказ \"{order.Title}\" прошёл модерацию и готов к выполнению.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        // Уведомляем заказчика, что заказ одобрен
        _context.Notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            Title = "Заказ одобрен",
            Text = $"Ваш заказ \"{order.Title}\" одобрен модератором. Исполнитель приступает к работе.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Заказ одобрен, работа начата" });
    }

    // POST: api/Moderation/orders/{id}/reject
    [HttpPost("orders/{id}/reject")]
    public async Task<IActionResult> RejectOrder(int id, [FromBody] RejectOrderDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Freelancer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Заказ не найден");

        if (order.Status != "PendingModeration")
            return BadRequest("Заказ не находится на модерации");

        // Возвращаем зарезервированные средства заказчику
        var customerAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == order.CustomerId);
        if (customerAccount != null)
        {
            customerAccount.Blocked -= order.Budget;
            customerAccount.Balance += order.Budget;
        }

        // Возвращаем заказ в статус Open и сбрасываем выбранного фрилансера
        order.Status = "Open";
        order.FreelancerId = null;

        // Уведомляем заказчика о причине отказа
        _context.Notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            Title = "Заказ отклонён модератором",
            Text = $"Ваш заказ \"{order.Title}\" отклонён. Причина: {dto.Reason}. Пожалуйста, отредактируйте заказ и отправьте на модерацию повторно.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Заказ отклонён, средства возвращены заказчику" });
    }
}

// DTO для причины отклонения
public class RejectOrderDto
{
    public string Reason { get; set; } = string.Empty;
}