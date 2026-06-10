using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.IO;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;
using FreelanceExchange.API.DTOs;

namespace FreelanceExchange.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public PortfolioController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyPortfolio()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.RoleId != 2)
            return Forbid("Только фрилансеры могут иметь портфолио");

        var items = await _context.Portfolios
            .Where(p => p.FreelancerId == userId)
            .Select(p => new PortfolioDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                Link = p.Link,
                ImageUrl = p.ImageUrl,
                CreatedAt = p.CreatedAt
            }).ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> AddPortfolioItem(CreatePortfolioDto dto)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.RoleId != 2)
            return Forbid("Только фрилансеры могут добавлять портфолио");

        var item = new Portfolio
        {
            Title = dto.Title,
            Description = dto.Description,
            Link = dto.Link,
            ImageUrl = dto.ImageUrl,
            FreelancerId = userId
        };

        _context.Portfolios.Add(item);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Проект добавлен в портфолио", Id = item.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePortfolioItem(int id, CreatePortfolioDto dto)
    {
        var userId = GetCurrentUserId();
        var item = await _context.Portfolios.FindAsync(id);
        if (item == null)
            return NotFound();

        if (item.FreelancerId != userId)
            return Forbid("Вы можете редактировать только свои проекты");

        item.Title = dto.Title;
        item.Description = dto.Description;
        item.Link = dto.Link;
        item.ImageUrl = dto.ImageUrl;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Проект обновлён" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePortfolioItem(int id)
    {
        var userId = GetCurrentUserId();
        var item = await _context.Portfolios.FindAsync(id);
        if (item == null)
            return NotFound();

        if (item.FreelancerId != userId)
            return Forbid("Вы можете удалять только свои проекты");

        _context.Portfolios.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Проект удалён" });
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не выбран");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "portfolio");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/portfolio/{uniqueFileName}";
        return Ok(new { ImageUrl = url });
    }

    [HttpPost("{id}/upload-result")]
    [Consumes("multipart/form-data")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> UploadResult(int id, [FromForm] IFormFile file)
    {
        var userId = GetCurrentUserId();
        var order = await _context.Orders.FindAsync(id);
        if (order == null || order.FreelancerId != userId || order.Status != "InProgress")
            return Forbid();

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "results");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/results/{uniqueFileName}";
        return Ok(new { FileUrl = url });
    }
}
