using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;
using FreelanceExchange.API.DTOs;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace FreelanceExchange.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public OrdersController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Account> GetOrCreateAccount(int userId)
    {
        var account = await _context.Accounts.FindAsync(userId);
        if (account == null)
        {
            account = new Account { UserId = userId };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
        }
        return account;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.RoleId != 2)
            return Forbid("Только заказчики могут создавать заказы");

        var category = await _context.Categories.FindAsync(dto.CategoryId);
        if (category == null)
            return BadRequest("Категория не найдена");

        var account = await GetOrCreateAccount(userId);
        if (account.Balance < dto.Budget)
            return BadRequest("Недостаточно средств для создания заказа");

        account.Balance -= dto.Budget;
        account.Blocked += dto.Budget;

        var order = new Order
        {
            Title = dto.Title,
            Description = dto.Description,
            Budget = dto.Budget,
            Deadline = dto.Deadline.ToUniversalTime(),
            Status = "Open",
            CreatedAt = DateTime.UtcNow,
            CustomerId = userId,
            CategoryId = dto.CategoryId
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Заказ создан, средства зарезервированы", OrderId = order.Id });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrders([FromQuery] OrderFilterDto filter)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = userIdClaim != null ? int.Parse(userIdClaim) : (int?)null;
        var user = userId.HasValue ? await _context.Users.FindAsync(userId.Value) : null;
        bool isAdminOrModerator = user?.RoleId == 5 || user?.RoleId == 4;

        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Category)
            .Include(o => o.Freelancer)
            .AsQueryable();

        if (user == null)
        {
            query = query.Where(o => o.Status == "Open");
        }
        else if (!isAdminOrModerator && user.RoleId == 2)
        {
            query = query.Where(o => o.CustomerId == userId.Value);
        }
        else if (!isAdminOrModerator && user.RoleId == 3)
        {
            query = query.Where(o => o.Status == "Open" ||
                                    ((o.Status == "InProgress" || o.Status == "Completed") && o.FreelancerId == userId.Value));
        }
        else if (!isAdminOrModerator)
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(o => o.Status == filter.Status);
        if (filter.CategoryId.HasValue)
            query = query.Where(o => o.CategoryId == filter.CategoryId.Value);
        if (filter.MinBudget.HasValue)
            query = query.Where(o => o.Budget >= filter.MinBudget.Value);
        if (filter.MaxBudget.HasValue)
            query = query.Where(o => o.Budget <= filter.MaxBudget.Value);
        if (!string.IsNullOrEmpty(filter.Search))
            query = query.Where(o => o.Title.Contains(filter.Search));
        if (filter.DeadlineDays.HasValue && filter.DeadlineDays.Value > 0)
        {
            var deadlineLimit = DateTime.UtcNow.AddDays(filter.DeadlineDays.Value);
            query = query.Where(o => o.Deadline <= deadlineLimit);
        }

        var totalCount = await query.CountAsync();
        var orders = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => new OrderResponseDto
            {
                Id = o.Id,
                Title = o.Title,
                Description = o.Description,
                Budget = o.Budget,
                Deadline = o.Deadline,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.FullName,
                CategoryId = o.CategoryId,
                CategoryName = o.Category.Name,
                FreelancerId = o.FreelancerId,
                FreelancerName = o.Freelancer != null ? o.Freelancer.FullName : null,
                FreelancerFileUrl = o.FreelancerFileUrl,
                CustomerFileUrl = o.CustomerFileUrl
            }).ToListAsync();

        return Ok(new { TotalCount = totalCount, Items = orders });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);

        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Category)
            .Include(o => o.Freelancer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        bool canView = user?.RoleId == 5 || user?.RoleId == 4 ||
                       (user?.RoleId == 2 && order.CustomerId == userId) ||
                       (user?.RoleId == 3 && order.FreelancerId == userId && (order.Status == "InProgress" || order.Status == "Completed"));
        
        if (!canView)
            return Forbid("У вас нет доступа к этому заказу");

        var dto = new OrderResponseDto
        {
            Id = order.Id,
            Title = order.Title,
            Description = order.Description,
            Budget = order.Budget,
            Deadline = order.Deadline,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer.FullName,
            CategoryId = order.CategoryId,
            CategoryName = order.Category.Name,
            FreelancerId = order.FreelancerId,
            FreelancerName = order.Freelancer?.FullName,
            FreelancerFileUrl = order.FreelancerFileUrl,
            CustomerFileUrl = order.CustomerFileUrl
        };

        return Ok(dto);
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(int id)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        bool canView = user?.RoleId == 5 || user?.RoleId == 4 ||
                       (user?.RoleId == 2 && order.CustomerId == userId) ||
                       (user?.RoleId == 3 && order.FreelancerId == userId && (order.Status == "InProgress" || order.Status == "Completed"));
        
        if (!canView) return Forbid();

        var messages = await _context.ChatMessages
            .Where(m => m.OrderId == id)
            .OrderBy(m => m.SentAt)
            .Select(m => new {
                m.Id,
                m.OrderId,
                m.SenderId,
                m.Message,
                m.SentAt,
                SenderName = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync();
        return Ok(messages);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrder(int id, CreateOrderDto dto)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.CustomerId != userId)
            return Forbid("Только автор заказа может редактировать");

        if (order.Status != "Open")
            return BadRequest("Нельзя редактировать заказ, уже принятый в работу");

        if (order.Budget != dto.Budget)
        {
            var account = await GetOrCreateAccount(userId);
            decimal diff = dto.Budget - order.Budget;
            if (diff > 0 && account.Balance < diff)
                return BadRequest("Недостаточно средств для увеличения бюджета");
            account.Balance -= diff;
            account.Blocked += diff;
        }

        order.Title = dto.Title;
        order.Description = dto.Description;
        order.Budget = dto.Budget;
        order.Deadline = dto.Deadline.ToUniversalTime();
        order.CategoryId = dto.CategoryId;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Заказ обновлён" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.CustomerId != userId)
            return Forbid("Только автор заказа может удалить");

        if (order.Status != "Open")
            return BadRequest("Нельзя удалить заказ, уже принятый в работу");

        var account = await GetOrCreateAccount(userId);
        account.Balance += order.Budget;
        account.Blocked -= order.Budget;

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Заказ удалён, средства возвращены" });
    }

    // ========== НАЗНАЧЕНИЕ ИСПОЛНИТЕЛЯ ==========
    [HttpPost("{id}/assign/{freelancerId}")]
    public async Task<IActionResult> AssignFreelancer(int id, int freelancerId)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        if (order.CustomerId != userId) return Forbid("Только заказчик может назначить исполнителя");
        if (order.Status != "Open") return BadRequest("Заказ уже в работе или завершён");

        var freelancer = await _context.Users.FindAsync(freelancerId);
        if (freelancer == null || freelancer.RoleId != 3)
            return BadRequest("Пользователь не является фрилансером");

        var freelancerAccount = await GetOrCreateAccount(freelancerId);
        freelancerAccount.Blocked += order.Budget;

        order.FreelancerId = freelancerId;
        order.Status = "InProgress";
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Исполнитель назначен, заказ переведён в статус 'В работе'" });
    }

    // ========== ЗАВЕРШЕНИЕ ЗАКАЗА ==========
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteOrder(int id)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Freelancer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();
        if (order.CustomerId != userId) return Forbid("Только заказчик может завершить заказ");
        if (order.Status != "InProgress") return BadRequest("Заказ не в работе. Сначала назначьте исполнителя.");
        if (order.FreelancerId == null) return BadRequest("У заказа нет исполнителя");

        var freelancerAccount = await GetOrCreateAccount(order.FreelancerId.Value);
        var customerAccount = await GetOrCreateAccount(order.CustomerId);
        
        var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 5);
        if (adminUser == null)
            return BadRequest("В системе нет администратора для сбора комиссии");

        var adminAccount = await GetOrCreateAccount(adminUser.Id);
        
        decimal commissionRate = 5.0m;
        decimal commission = order.Budget * (commissionRate / 100);
        decimal freelancerPayout = order.Budget - commission;

        // Переводим средства
        freelancerAccount.Blocked -= order.Budget;
        freelancerAccount.Balance += freelancerPayout;
        customerAccount.Blocked -= order.Budget;
        adminAccount.Balance += commission;

        order.Status = "Completed";

        // Логируем транзакции
        _context.Transactions.Add(new Transaction
        {
            InitiatorId = userId,
            Amount = -order.Budget,
            Type = "OrderComplete",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            OrderId = order.Id
        });
        _context.Transactions.Add(new Transaction
        {
            InitiatorId = order.FreelancerId.Value,
            Amount = freelancerPayout,
            Type = "FreelancerPayment",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            OrderId = order.Id
        });
        _context.Transactions.Add(new Transaction
        {
            InitiatorId = adminUser.Id,
            Amount = commission,
            Type = "PlatformCommission",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            OrderId = order.Id
        });

        await _context.SaveChangesAsync();

        // Уведомление фрилансеру
        _context.Notifications.Add(new Notification
        {
            UserId = order.FreelancerId.Value,
            Title = "Заказ завершён",
            Text = $"Заказ \"{order.Title}\" завершён. На ваш счёт поступило {freelancerPayout:F2} BYN (комиссия платформы: {commission:F2} BYN).",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        await _context.SaveChangesAsync();

        return Ok(new { Message = $"Заказ завершён, фрилансеру перечислено {freelancerPayout:F2} BYN (комиссия {commission:F2} BYN)" });
    }

    private async Task<string> SaveFile(IFormFile file, string subFolder)
    {
        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subFolder);
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);
        return $"/uploads/{subFolder}/{fileName}";
    }

    [HttpPost("{id}/upload-freelancer")]
    public async Task<IActionResult> UploadFreelancerFile(int id, IFormFile file)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        if (order.FreelancerId != userId || order.Status != "InProgress")
            return Forbid();

        var url = await SaveFile(file, "results");
        order.FreelancerFileUrl = url;
        await _context.SaveChangesAsync();
        return Ok(new { FileUrl = url });
    }

    [HttpPost("{id}/upload-customer")]
    public async Task<IActionResult> UploadCustomerFile(int id, IFormFile file)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        if (order.CustomerId != userId || order.Status != "InProgress")
            return Forbid();

        var url = await SaveFile(file, "results");
        order.CustomerFileUrl = url;
        await _context.SaveChangesAsync();
        return Ok(new { FileUrl = url });
    }
}