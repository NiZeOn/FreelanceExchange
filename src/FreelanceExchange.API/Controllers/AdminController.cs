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
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ========== Публичные методы ==========
    [HttpGet("public/categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicCategories()
    {
        var categories = await _context.Categories.ToListAsync();
        return Ok(categories);
    }

    // ========== Управление пользователями (Admin и Moderator) ==========
    [HttpGet("users")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.Phone,
                u.RegistrationDate,
                u.IsBlocked,
                Role = u.Role.Name,
                RoleId = u.RoleId
            }).ToListAsync();
        return Ok(users);
    }

    [HttpPut("users/{id}/block")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> BlockUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsBlocked = true;
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Пользователь заблокирован" });
    }

    [HttpPut("users/{id}/unblock")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> UnblockUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsBlocked = false;
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Пользователь разблокирован" });
    }

    [HttpPut("users/{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeUserRole(int id, [FromBody] UserRoleUpdateDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        var role = await _context.Roles.FindAsync(dto.NewRoleId);
        if (role == null) return BadRequest("Роль не существует");
        user.RoleId = dto.NewRoleId;
        await _context.SaveChangesAsync();
        return Ok(new { Message = $"Роль изменена на {role.Name}" });
    }

    // ========== Управление категориями (только Admin) ==========
    [HttpGet("categories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllCategories()
    {
        var categories = await _context.Categories.ToListAsync();
        return Ok(categories);
    }

    [HttpPost("categories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCategory(CreateCategoryDto dto)
    {
        var category = new Category { Name = dto.Name, Description = dto.Description };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Категория создана", CategoryId = category.Id });
    }

    [HttpPut("categories/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCategory(int id, CreateCategoryDto dto)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        category.Name = dto.Name;
        category.Description = dto.Description;
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Категория обновлена" });
    }

    [HttpDelete("categories/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Категория удалена" });
    }

    // ========== Управление навыками (только Admin) ==========
    [HttpGet("skills")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSkills()
    {
        var skills = await _context.Skills.ToListAsync();
        return Ok(skills);
    }

    [HttpPost("skills")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSkill([FromBody] SkillDto dto)
    {
        var skill = new Skill { Name = dto.Name };
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Навык создан", SkillId = skill.Id });
    }

    [HttpPut("skills/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSkill(int id, [FromBody] SkillDto dto)
    {
        var skill = await _context.Skills.FindAsync(id);
        if (skill == null) return NotFound();
        skill.Name = dto.Name;
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Навык обновлён" });
    }

    [HttpDelete("skills/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSkill(int id)
    {
        var skill = await _context.Skills.FindAsync(id);
        if (skill == null) return NotFound();
        _context.Skills.Remove(skill);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Навык удалён" });
    }

    // ========== Статистика (Admin и Moderator) ==========
    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalOrders = await _context.Orders.CountAsync();
        var completedOrders = await _context.Orders.CountAsync(o => o.Status == "Completed");
        var totalTurnover = await _context.Orders.Where(o => o.Status == "Completed").SumAsync(o => o.Budget);
        
        // Фиксированная комиссия 5%
        decimal commissionRate = 5.0m;
        var totalCommission = totalTurnover * (commissionRate / 100);
        
        return Ok(new AdminStatsDto
        {
            TotalUsers = totalUsers,
            TotalOrders = totalOrders,
            CompletedOrders = completedOrders,
            TotalTurnover = totalTurnover,
            TotalCommission = totalCommission
        });
    }

    // ========== Статистика комиссии (только Admin) ==========
    [HttpGet("commission-stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCommissionStats()
    {
        var totalCommission = await _context.Transactions
            .Where(t => t.Type == "PlatformCommission")
            .SumAsync(t => t.Amount);
        
        var monthlyCommission = await _context.Transactions
            .Where(t => t.Type == "PlatformCommission" && t.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
            .SumAsync(t => t.Amount);

        return Ok(new
        {
            totalCommission = totalCommission,
            monthlyCommission = monthlyCommission,
            currency = "BYN"
        });
    }

    // ========== Детальный отчёт по комиссиям (только Admin) ==========
    [HttpGet("commission-transactions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCommissionTransactions([FromQuery] string startDate, [FromQuery] string endDate)
    {
        if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            return BadRequest("Укажите startDate и endDate в формате YYYY-MM-DD");

        var start = DateTime.Parse(startDate).ToUniversalTime();
        var end = DateTime.Parse(endDate).ToUniversalTime().AddDays(1).AddTicks(-1);

        var transactions = await _context.Transactions
            .Include(t => t.Order)
                .ThenInclude(o => o.Customer)
            .Include(t => t.Order)
                .ThenInclude(o => o.Freelancer)
            .Where(t => t.Type == "PlatformCommission" && t.CreatedAt >= start && t.CreatedAt <= end)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.OrderId,
                t.Amount,
                Date = t.CreatedAt,
                OrderTitle = t.Order != null ? t.Order.Title : null,
                CustomerName = t.Order != null && t.Order.Customer != null ? t.Order.Customer.FullName : null,
                FreelancerName = t.Order != null && t.Order.Freelancer != null ? t.Order.Freelancer.FullName : null
            })
            .ToListAsync();

        return Ok(transactions);
    }

    // ========== Статистика заказов по месяцам ==========
    [HttpGet("orders-by-month")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetOrdersByMonth()
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddMonths(-5).AddDays(-now.Day + 1).Date;
        var months = new List<string>();
        var counts = new List<int>();

        for (int i = 0; i < 6; i++)
        {
            var monthStart = startDate.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);
            var count = await _context.Orders
                .Where(o => o.Status == "Completed" && o.CreatedAt >= monthStart && o.CreatedAt < monthEnd)
                .CountAsync();
            months.Add(monthStart.ToString("MMM yyyy"));
            counts.Add(count);
        }

        return Ok(new { months, counts });
    }

    // ========== Тарифы подписок (Admin и Moderator – просмотр, редактирование только Admin) ==========
    [HttpGet("subscriptions")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetSubscriptionPlans()
    {
        var plans = await _context.SubscriptionPlans.ToListAsync();
        return Ok(plans);
    }

    [HttpPost("subscriptions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSubscriptionPlan([FromBody] SubscriptionPlanDto dto)
    {
        var plan = new SubscriptionPlan
        {
            Name = dto.Name,
            Price = dto.Price,
            Days = dto.Days,
            TargetRole = dto.TargetRole ?? "Freelancer"
        };
        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Тариф создан", Id = plan.Id });
    }

    [HttpPut("subscriptions/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSubscriptionPlan(int id, [FromBody] SubscriptionPlanDto dto)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();
        plan.Name = dto.Name;
        plan.Price = dto.Price;
        plan.Days = dto.Days;
        plan.TargetRole = dto.TargetRole ?? "Freelancer";
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Тариф обновлён" });
    }

    [HttpDelete("subscriptions/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSubscriptionPlan(int id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound();
        _context.SubscriptionPlans.Remove(plan);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Тариф удалён" });
    }


    // ========== Массовые уведомления (только Admin) ==========
    [HttpPost("notifications")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendMassNotification([FromBody] NotificationDto dto)
    {
        var users = await _context.Users.ToListAsync();
        foreach (var user in users)
        {
            var notification = new Notification
            {
                Text = $"{dto.Subject}\n{dto.Body}",
                UserId = user.Id
            };
            _context.Notifications.Add(notification);
        }
        await _context.SaveChangesAsync();
        return Ok(new { Message = $"Уведомление '{dto.Subject}' отправлено всем {users.Count} пользователям" });
    }
}