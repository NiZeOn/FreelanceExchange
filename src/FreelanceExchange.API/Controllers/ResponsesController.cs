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
[Authorize]
public class ResponsesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ResponsesController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> CreateResponse(CreateResponseDto dto)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.RoleId != 3)
            return Forbid("Только фрилансеры могут откликаться на заказы");

        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null)
            return BadRequest("Заказ не найден");

        if (order.Status != "Open")
            return BadRequest("На этот заказ уже нельзя откликнуться");

        var existingResponse = await _context.Responses
            .FirstOrDefaultAsync(r => r.FreelancerId == userId && r.OrderId == dto.OrderId);
        if (existingResponse != null)
            return BadRequest("Вы уже откликнулись на этот заказ");

        var response = new Response
        {
            CoverLetter = dto.CoverLetter,
            ProposedPrice = dto.ProposedPrice,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending",
            FreelancerId = userId,
            OrderId = dto.OrderId
        };

        _context.Responses.Add(response);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Отклик отправлен", ResponseId = response.Id });
    }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetResponsesForOrder(int orderId)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();

        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return NotFound("Заказ не найден");

        if (order.CustomerId != userId && user.RoleId != 5)
            return Forbid("У вас нет прав просматривать отклики на этот заказ");

        var responses = await _context.Responses
            .Include(r => r.Freelancer)
            .Where(r => r.OrderId == orderId)
            .Select(r => new ResponseDto
            {
                Id = r.Id,
                CoverLetter = r.CoverLetter,
                ProposedPrice = r.ProposedPrice,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                FreelancerId = r.FreelancerId,
                FreelancerName = r.Freelancer.FullName,
                FreelancerAvatarUrl = r.Freelancer.AvatarUrl,
                OrderId = r.OrderId,
                OrderTitle = order.Title
            }).ToListAsync();

        return Ok(responses);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyResponses()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.RoleId != 3)
            return Forbid("Только фрилансеры могут просматривать свои отклики");

        var responses = await _context.Responses
            .Include(r => r.Order)
            .Where(r => r.FreelancerId == userId)
            .Select(r => new ResponseDto
            {
                Id = r.Id,
                CoverLetter = r.CoverLetter,
                ProposedPrice = r.ProposedPrice,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                FreelancerId = r.FreelancerId,
                FreelancerName = user.FullName,
                FreelancerAvatarUrl = user.AvatarUrl,
                OrderId = r.OrderId,
                OrderTitle = r.Order.Title
            }).ToListAsync();

        return Ok(responses);
    }

    [HttpPost("{id}/accept")]
    public async Task<IActionResult> AcceptResponse(int id)
    {
        var userId = GetCurrentUserId();
        var response = await _context.Responses
            .Include(r => r.Order)
            .Include(r => r.Freelancer)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (response == null)
            return NotFound("Отклик не найден");

        var order = response.Order;
        if (order.CustomerId != userId)
            return Forbid("Только заказчик может принять отклик");

        if (order.Status != "Open")
            return BadRequest("Этот заказ уже обработан");

        if (response.Status != "Pending")
            return BadRequest("Этот отклик уже принят или отклонён");

        // Принимаем отклик, но заказ отправляем на модерацию
        response.Status = "Selected";           // новый статус для выбранного отклика
        order.Status = "PendingModeration";     // ожидает проверки модератором
        order.FreelancerId = response.FreelancerId;

        // Отклоняем все остальные отклики на этот заказ
        var otherResponses = await _context.Responses
            .Where(r => r.OrderId == order.Id && r.Id != id && r.Status == "Pending")
            .ToListAsync();

        foreach (var r in otherResponses)
        {
            r.Status = "Rejected";

            // Уведомление каждому отклонённому фрилансеру
            _context.Notifications.Add(new Notification
            {
                UserId = r.FreelancerId,
                Title = "Отклик отклонён",
                Text = $"Ваш отклик на заказ \"{order.Title}\" был отклонён заказчиком.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        // Уведомление заказчику, что заказ отправлен на модерацию
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = "Заказ на модерации",
            Text = $"Ваш заказ \"{order.Title}\" отправлен на проверку модератору. После одобрения фрилансер приступит к работе.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        // (Уведомление фрилансеру будет отправлено после одобрения модератором)

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Отклик принят, заказ отправлен на модерацию" });
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectResponse(int id)
    {
        var userId = GetCurrentUserId();
        var response = await _context.Responses
            .Include(r => r.Order)
            .Include(r => r.Freelancer)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (response == null)
            return NotFound("Отклик не найден");

        var order = response.Order;
        if (order.CustomerId != userId)
            return Forbid("Только заказчик может отклонить отклик");

        if (order.Status != "Open")
            return BadRequest("Этот заказ уже обработан");

        if (response.Status != "Pending")
            return BadRequest("Этот отклик уже принят или отклонён");

        response.Status = "Rejected";

        // Уведомление фрилансеру об отклонении
        _context.Notifications.Add(new Notification
        {
            UserId = response.FreelancerId,
            Title = "Отклик отклонён",
            Text = $"Ваш отклик на заказ \"{order.Title}\" был отклонён заказчиком.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Отклик отклонён" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteResponse(int id)
    {
        var userId = GetCurrentUserId();
        var response = await _context.Responses.FindAsync(id);
        if (response == null)
            return NotFound();

        if (response.FreelancerId != userId)
            return Forbid("Вы можете удалить только свой отклик");

        if (response.Status != "Pending")
            return BadRequest("Нельзя удалить уже принятый или отклонённый отклик");

        _context.Responses.Remove(response);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Отклик удалён" });
    }
}