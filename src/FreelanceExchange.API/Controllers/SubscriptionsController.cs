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
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(AppDbContext context, ILogger<SubscriptionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET: api/Subscriptions/plans
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans()
    {
        // Возвращаем все тарифы без фильтрации – фильтрация по роли будет на клиенте
        var plans = await _context.SubscriptionPlans.ToListAsync();
        return Ok(plans);
    }

    // GET: api/Subscriptions/my
    [HttpGet("my")]
    public async Task<IActionResult> GetMySubscription()
    {
        var userId = GetUserId();
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.EndDate >= DateTime.UtcNow)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        if (subscription == null)
            return Ok(new { hasActiveSubscription = false });

        return Ok(new
        {
            hasActiveSubscription = true,
            planName = subscription.Plan?.Name,
            startDate = subscription.StartDate,
            endDate = subscription.EndDate,
            daysLeft = (int)(subscription.EndDate - DateTime.UtcNow).TotalDays
        });
    }

    // POST: api/Subscriptions/buy
    [HttpPost("buy")]
    public async Task<IActionResult> BuySubscription([FromBody] BuySubscriptionDto dto)
    {
        var userId = GetUserId();
        var plan = await _context.SubscriptionPlans.FindAsync(dto.PlanId);
        if (plan == null)
            return BadRequest("Тариф не найден");

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        if (!string.IsNullOrEmpty(plan.TargetRole) && plan.TargetRole != user.Role.Name)
            return BadRequest("Этот тариф не предназначен для вашей роли");

        var existingActive = await _context.Subscriptions
            .AnyAsync(s => s.UserId == userId && s.EndDate >= DateTime.UtcNow);
        if (existingActive)
            return BadRequest("У вас уже есть активная подписка. Дождитесь её окончания или обратитесь в поддержку.");

        var finance = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (finance == null || finance.Balance < plan.Price)
            return BadRequest($"Недостаточно средств. Необходимо {plan.Price} BYN, доступно {finance?.Balance ?? 0} BYN");

        finance.Balance -= plan.Price;

        _context.Transactions.Add(new Transaction
        {
            InitiatorId = userId,
            Amount = -plan.Price,
            Type = "SubscriptionPurchase",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });

        var subscription = new Subscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Name = plan.Name,
            Price = plan.Price,
            Days = plan.Days,
            Type = user.Role.Name == "Freelancer" ? "FreelancerPro" : "CustomerPro",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(plan.Days)
        };
        _context.Subscriptions.Add(subscription);

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Подписка на тариф '{plan.Name}' успешно оформлена до {subscription.EndDate:yyyy-MM-dd}" });
    }
}

public class BuySubscriptionDto
{
    public int PlanId { get; set; }
}