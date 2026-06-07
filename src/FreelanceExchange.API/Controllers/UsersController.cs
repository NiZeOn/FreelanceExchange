using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace FreelanceExchange.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public UsersController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ========== ЛИЧНЫЙ ПРОФИЛЬ (текущего пользователя) ==========
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        return Ok(new
        {
            user.Skills,
            user.HourlyRate,
            user.Bio,
            user.AvatarUrl,
            user.Email,
            user.FullName,
            user.Phone
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (!string.IsNullOrEmpty(dto.FullName))
            user.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Phone))
            user.Phone = dto.Phone;
        if (dto.Skills != null)
            user.Skills = dto.Skills;
        if (dto.HourlyRate.HasValue)
            user.HourlyRate = dto.HourlyRate;
        if (dto.Bio != null)
            user.Bio = dto.Bio;

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile avatar)
    {
        if (avatar == null || avatar.Length == 0)
            return BadRequest("Файл не выбран");
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var ext = Path.GetExtension(avatar.FileName).ToLower();
        if (!allowed.Contains(ext))
            return BadRequest("Недопустимый формат");
        var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await avatar.CopyToAsync(stream);
        }
        var avatarUrl = $"/avatars/{fileName}";
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        user.AvatarUrl = avatarUrl;
        await _context.SaveChangesAsync();
        return Ok(new { AvatarUrl = avatarUrl });
    }

    [HttpPut("email")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto dto)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        // В реальном проекте проверяйте пароль!
        user.Email = dto.NewEmail;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        // Здесь должно быть хеширование пароля (BCrypt). Для примера – сохраняем как есть.
        user.PasswordHash = dto.NewPassword;
        await _context.SaveChangesAsync();
        return Ok();
    }

    // ========== ПУБЛИЧНЫЙ ПРОФИЛЬ (просмотр одного пользователя) ==========
    [HttpGet("{id}/public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProfile(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var reviews = await _context.Reviews
            .Where(r => r.RecipientId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Comment,
                r.CreatedAt,
                AuthorName = r.Author.FullName,
                AuthorAvatar = r.Author.AvatarUrl
            })
            .ToListAsync();

        double averageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

        List<object> portfolio = new();
        if (user.Role.Name == "Freelancer")
        {
            portfolio = await _context.Portfolios
                .Where(p => p.FreelancerId == id)
                .Select(p => new { p.Id, p.Title, p.Description, p.Link, p.ImageUrl })
                .ToListAsync<object>();
        }

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.AvatarUrl,
            Role = user.Role.Name,
            AverageRating = Math.Round(averageRating, 1),
            TotalReviews = reviews.Count,
            Reviews = reviews,
            Portfolio = portfolio,
            Skills = user.Skills ?? string.Empty,
            HourlyRate = user.HourlyRate,
            Bio = user.Bio ?? string.Empty
        });
    }

    // ========== ПУБЛИЧНЫЙ СПИСОК ФРИЛАНСЕРОВ (для каталога) ==========
    [HttpGet("freelancers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFreelancers()
    {
        var freelancers = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Role.Name == "Freelancer" && !u.IsBlocked)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.AvatarUrl,
                u.Skills,
                u.HourlyRate,
                u.Bio,
                AverageRating = _context.Reviews
                    .Where(r => r.RecipientId == u.Id)
                    .Average(r => (double?)r.Rating) ?? 0,
                TotalReviews = _context.Reviews.Count(r => r.RecipientId == u.Id)
            })
            .ToListAsync();

        return Ok(freelancers);
    }

    // ========== ПУБЛИЧНЫЙ СПИСОК НАВЫКОВ (для автодополнения фрилансерами) ==========
    [HttpGet("skills")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllSkills()
    {
        var skills = await _context.Skills
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync();
        return Ok(skills);
    }
}

// ========== DTO ==========
public class UpdateProfileDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Skills { get; set; }
    public decimal? HourlyRate { get; set; }
    public string? Bio { get; set; }
}

public class ChangeEmailDto
{
    public string NewEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}