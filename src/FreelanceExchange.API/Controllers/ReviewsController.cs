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
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // POST: api/Reviews
    [HttpPost]
    public async Task<IActionResult> CreateReview(CreateReviewDto dto)
    {
        var userId = GetCurrentUserId();

        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null) return NotFound("Заказ не найден");
        if (order.Status != "Completed")
            return BadRequest("Отзыв можно оставить только на завершённый заказ");

        if (order.CustomerId != userId && order.FreelancerId != userId)
            return Forbid("Вы не участвовали в этом заказе");

        int recipientId = order.CustomerId == userId ? order.FreelancerId!.Value : order.CustomerId;
        if (recipientId == userId) return BadRequest("Нельзя оставить отзыв самому себе");

        var existing = await _context.Reviews
            .FirstOrDefaultAsync(r => r.OrderId == dto.OrderId && r.AuthorId == userId);
        if (existing != null) return BadRequest("Вы уже оставили отзыв на этот заказ");

        var review = new Review
        {
            Rating = dto.Rating,
            Comment = dto.Comment,
            AuthorId = userId,
            RecipientId = recipientId,
            OrderId = dto.OrderId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        // Обновляем рейтинг пользователя (можно добавить поле AverageRating в User)
        var recipient = await _context.Users.FindAsync(recipientId);
        var avgRating = await _context.Reviews
            .Where(r => r.RecipientId == recipientId)
            .AverageAsync(r => r.Rating);
        // Если есть поле AverageRating в User, обновить:
        // recipient.AverageRating = (decimal)avgRating;
        // await _context.SaveChangesAsync();

        return Ok(new { Message = "Отзыв оставлен" });
    }

    // GET: api/Reviews/user/{userId}?page=1&pageSize=10
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserReviews(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _context.Reviews
            .Include(r => r.Author)
            .Include(r => r.Order)
            .Where(r => r.RecipientId == userId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync();
        var reviews = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                AuthorId = r.AuthorId,
                AuthorName = r.Author.FullName,
                RecipientId = r.RecipientId,
                RecipientName = "", // можно не заполнять
                OrderTitle = r.Order != null ? r.Order.Title : null
            })
            .ToListAsync();

        var averageRating = totalCount > 0 ? await query.AverageAsync(r => r.Rating) : 0;

        return Ok(new
        {
            totalCount,
            averageRating = Math.Round(averageRating, 1),
            reviews
        });
    }

    // GET: api/Reviews/my?page=1&pageSize=10
    [HttpGet("my")]
    public async Task<IActionResult> GetMyReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();
        var query = _context.Reviews
            .Include(r => r.Author)
            .Include(r => r.Order)
            .Where(r => r.RecipientId == userId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync();
        var reviews = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                AuthorId = r.AuthorId,
                AuthorName = r.Author.FullName,
                RecipientId = r.RecipientId,
                RecipientName = "",
                OrderTitle = r.Order != null ? r.Order.Title : null
            })
            .ToListAsync();

        var averageRating = totalCount > 0 ? await query.AverageAsync(r => r.Rating) : 0;

        return Ok(new
        {
            totalCount,
            averageRating = Math.Round(averageRating, 1),
            reviews
        });
    }
}