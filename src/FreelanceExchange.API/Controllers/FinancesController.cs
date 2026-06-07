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
public class FinancesController : ControllerBase
{
    private readonly AppDbContext _context;

    public FinancesController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAccount()
    {
        var userId = GetCurrentUserId();
        var account = await _context.Accounts.FindAsync(userId);
        if (account == null)
        {
            account = new Account { UserId = userId };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
        }
        return Ok(new { account.Balance, account.Blocked });
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositDto dto)
    {
        if (dto.Amount <= 0) return BadRequest("Сумма должна быть положительной");
        var userId = GetCurrentUserId();
        var account = await GetOrCreateAccount(userId);
        account.Balance += dto.Amount;
        
        // Добавляем запись о транзакции
        var transaction = new Transaction
        {
            Type = "Deposit",
            Amount = dto.Amount,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            InitiatorId = userId
        };
        _context.Transactions.Add(transaction);
        
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Баланс пополнен", NewBalance = account.Balance });
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawDto dto)
    {
        if (dto.Amount <= 0) return BadRequest("Сумма должна быть положительной");
        var userId = GetCurrentUserId();
        var account = await GetOrCreateAccount(userId);
        if (account.Balance < dto.Amount) return BadRequest("Недостаточно средств");
        account.Balance -= dto.Amount;
        
        // Добавляем запись о транзакции
        var transaction = new Transaction
        {
            Type = "Withdraw",
            Amount = -dto.Amount, // отрицательная сумма для вывода
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            InitiatorId = userId
        };
        _context.Transactions.Add(transaction);
        
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Средства выведены", NewBalance = account.Balance });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions()
    {
        var userId = GetCurrentUserId();
        var transactions = await _context.Transactions
            .Where(t => t.InitiatorId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.Type, t.Amount, Date = t.CreatedAt })
            .ToListAsync();
        return Ok(transactions);
    }

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
}

public class DepositDto { public decimal Amount { get; set; } }
public class WithdrawDto { public decimal Amount { get; set; } }