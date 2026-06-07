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
public class DisputesController : ControllerBase
{
    private readonly AppDbContext _context;

    public DisputesController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Создание диспута (доступно заказчику или фрилансеру, если заказ InProgress и диспут ещё не открыт)
    [HttpPost]
    public async Task<IActionResult> CreateDispute(CreateDisputeDto dto)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null) return NotFound("Заказ не найден");
        if (order.CustomerId != userId && order.FreelancerId != userId)
            return Forbid("Вы не являетесь участником этого заказа");
        if (order.Status != "InProgress")
            return BadRequest("Диспут можно открыть только по заказу в статусе 'В работе'");

        var existingDispute = await _context.Disputes.FirstOrDefaultAsync(d => d.OrderId == dto.OrderId && d.Status != "Resolved");
        if (existingDispute != null)
            return BadRequest("Диспут по этому заказу уже открыт");

        var dispute = new Dispute
        {
            OrderId = dto.OrderId,
            InitiatorId = userId,
            Reason = dto.Reason,
            Status = "Open"
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Диспут открыт, ожидайте решения модератора" });
    }

    // Получить диспут по заказу (для участников и модератора/админа)
    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetDisputeByOrder(int orderId)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        bool isParticipant = order.CustomerId == userId || order.FreelancerId == userId;
        bool isAdminOrModerator = user?.RoleId == 5 || user?.RoleId == 4;
        if (!isParticipant && !isAdminOrModerator)
            return Forbid();

        var dispute = await _context.Disputes
            .Include(d => d.Initiator)
            .Include(d => d.Moderator)
            .FirstOrDefaultAsync(d => d.OrderId == orderId);
        if (dispute == null) return Ok(null);
        var dto = new DisputeResponseDto
        {
            Id = dispute.Id,
            OrderId = dispute.OrderId,
            OrderTitle = order.Title,
            InitiatorId = dispute.InitiatorId,
            InitiatorName = dispute.Initiator.FullName,
            Reason = dispute.Reason,
            OpenedAt = dispute.OpenedAt,
            Resolution = dispute.Resolution,
            Status = dispute.Status,
            ModeratorId = dispute.ModeratorId,
            ModeratorName = dispute.Moderator?.FullName
        };
        return Ok(dto);
    }

    // Получить все диспуты (для модератора/админа)
    [HttpGet]
    [Authorize(Roles = "Moderator,Admin")]
    public async Task<IActionResult> GetAllDisputes()
    {
        var disputes = await _context.Disputes
            .Include(d => d.Order)
            .Include(d => d.Initiator)
            .OrderByDescending(d => d.OpenedAt)
            .Select(d => new DisputeResponseDto
            {
                Id = d.Id,
                OrderId = d.OrderId,
                OrderTitle = d.Order.Title,
                InitiatorId = d.InitiatorId,
                InitiatorName = d.Initiator.FullName,
                Reason = d.Reason,
                OpenedAt = d.OpenedAt,
                Resolution = d.Resolution,
                Status = d.Status
            }).ToListAsync();
        return Ok(disputes);
    }

    // Разрешение диспута (только для модератора/админа)
    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "Moderator,Admin")]
    public async Task<IActionResult> ResolveDispute(int id, ResolveDisputeDto dto)
    {
        var moderatorId = GetCurrentUserId();
        var dispute = await _context.Disputes
            .Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dispute == null) return NotFound();
        if (dispute.Status != "Open") return BadRequest("Диспут уже разрешён");

        var order = dispute.Order;
        if (dto.Resolution == "Customer")
        {
            // Возвращаем средства заказчику
            var customerAccount = await _context.Accounts.FindAsync(order.CustomerId);
            if (customerAccount != null)
            {
                customerAccount.Balance += order.Budget;
                customerAccount.Blocked -= order.Budget;
            }
            // Средства фрилансеру не перечисляются
            dispute.Resolution = "В пользу заказчика";
            order.Status = "Cancelled";
        }
        else if (dto.Resolution == "Freelancer")
        {
            // Переводим средства фрилансеру
            var freelancerAccount = await _context.Accounts.FindAsync(order.FreelancerId!.Value);
            var customerAccount = await _context.Accounts.FindAsync(order.CustomerId);
            if (freelancerAccount != null)
            {
                freelancerAccount.Balance += order.Budget;
                customerAccount!.Blocked -= order.Budget;
            }
            dispute.Resolution = "В пользу фрилансера";
            order.Status = "Completed";
        }
        else
        {
            return BadRequest("Resolution должно быть 'Customer' или 'Freelancer'");
        }

        dispute.Status = "Resolved";
        dispute.ModeratorId = moderatorId;
        dispute.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Диспут разрешён" });
    }
}