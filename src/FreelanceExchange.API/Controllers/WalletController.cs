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
public class WalletController : ControllerBase
{
    private readonly AppDbContext _context;

    public WalletController(AppDbContext context)
    {
        _context = context;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET: api/Wallet/my
    [HttpGet("my")]
    public async Task<IActionResult> GetMyWallet()
    {
        var userId = GetUserId();
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0 };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }
        return Ok(new
        {
            balance = wallet.Balance,
            lastFourDigits = wallet.LastFourDigits,
            hasCard = !string.IsNullOrEmpty(wallet.LastFourDigits)
        });
    }

    // POST: api/Wallet/link-card
    [HttpPost("link-card")]
    public async Task<IActionResult> LinkCard([FromBody] LinkCardDto dto)
    {
        var userId = GetUserId();
        var cardNumberClean = (dto.CardNumber ?? "").Replace(" ", "");
        if (!System.Text.RegularExpressions.Regex.IsMatch(cardNumberClean, @"^\d{16}$"))
            return BadRequest(new { error = "Неверный номер карты" });

        if (string.IsNullOrEmpty(dto.Expiry) || !System.Text.RegularExpressions.Regex.IsMatch(dto.Expiry, @"^\d{2}/\d{2}$"))
            return BadRequest(new { error = "Неверный срок действия (MM/YY)" });

        if (string.IsNullOrEmpty(dto.Cvc) || !System.Text.RegularExpressions.Regex.IsMatch(dto.Cvc, @"^\d{3}$"))
            return BadRequest(new { error = "CVC должен содержать 3 цифры" });

        // Получаем или создаём кошелёк
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0 };
            _context.Wallets.Add(wallet);
        }

        wallet.LastFourDigits = cardNumberClean[^4..]; // последние 4 цифры
        wallet.CardToken = Guid.NewGuid().ToString();
        wallet.UpdatedAt = DateTime.UtcNow;

        // Разбираем срок действия карты
        var parts = dto.Expiry.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out var month) && int.TryParse(parts[1], out var year))
        {
            // Сохраняем как UTC (первое число месяца)
            wallet.CardExpiry = new DateTime(2000 + year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            wallet.CardExpiry = null; // если модель nullable
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Карта привязана", lastFourDigits = wallet.LastFourDigits });
    }

    // POST: api/Wallet/deposit
    [HttpPost("deposit")]
    public async Task<IActionResult> DepositToWallet([FromBody] WalletDepositDto dto)
    {
        if (dto.Amount <= 0) return BadRequest(new { error = "Сумма должна быть положительной" });

        var userId = GetUserId();
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0 };
            _context.Wallets.Add(wallet);
        }

        wallet.Balance += dto.Amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        _context.Transactions.Add(new Transaction
        {
            InitiatorId = userId,
            Amount = dto.Amount,
            Type = "WalletDeposit",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { newBalance = wallet.Balance, message = $"Кошелёк пополнен на {dto.Amount} BYN" });
    }

    // POST: api/Wallet/transfer-to-platform
    [HttpPost("transfer-to-platform")]
    public async Task<IActionResult> TransferToPlatform([FromBody] TransferDto dto)
    {
        if (dto.Amount <= 0) return BadRequest(new { error = "Сумма должна быть положительной" });

        var userId = GetUserId();
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) return BadRequest(new { error = "Кошелёк не найден" });
        if (wallet.Balance < dto.Amount) return BadRequest(new { error = "Недостаточно средств на кошельке" });

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null)
        {
            account = new Account { UserId = userId };
            _context.Accounts.Add(account);
        }

        wallet.Balance -= dto.Amount;
        account.Balance += dto.Amount;

        _context.Transactions.Add(new Transaction
        {
            InitiatorId = userId,
            Amount = -dto.Amount,
            Type = "TransferToPlatform",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });
        _context.Transactions.Add(new Transaction
        {
            InitiatorId = userId,
            Amount = dto.Amount,
            Type = "PlatformDeposit",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { walletBalance = wallet.Balance, platformBalance = account.Balance });
    }

    // POST: api/Wallet/withdraw-to-wallet
    [HttpPost("withdraw-to-wallet")]
    public async Task<IActionResult> WithdrawToWallet([FromBody] TransferDto dto)
    {
        if (dto.Amount <= 0) return BadRequest(new { error = "Сумма должна быть положительной" });

        var userId = GetUserId();
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (account == null || account.Balance < dto.Amount)
            return BadRequest(new { error = "Недостаточно средств на платформе" });

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0 };
            _context.Wallets.Add(wallet);
        }

        account.Balance -= dto.Amount;
        wallet.Balance += dto.Amount;

        _context.Transactions.Add(new Transaction
        {
            InitiatorId = userId,
            Amount = dto.Amount,
            Type = "WithdrawToWallet",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { walletBalance = wallet.Balance, platformBalance = account.Balance });
    }
}

// DTO
public class LinkCardDto
{
    public string CardNumber { get; set; } = "";
    public string Expiry { get; set; } = "";
    public string Cvc { get; set; } = "";
}

public class WalletDepositDto
{
    public decimal Amount { get; set; }
    public string CardNumber { get; set; } = "";
    public string Expiry { get; set; } = "";
    public string Cvc { get; set; } = "";
}

public class TransferDto
{
    public decimal Amount { get; set; }
}