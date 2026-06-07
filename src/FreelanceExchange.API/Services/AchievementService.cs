using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FreelanceExchange.API.Services;

public class AchievementService
{
    private readonly AppDbContext _context;

    public AchievementService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Проверить и выдать достижение пользователю по триггеру
    /// </summary>
    public async Task CheckAndAwardAsync(int userId, string triggerType, int currentCount = 1)
    {
        var achievements = await _context.Achievements
            .Where(a => a.TriggerType == triggerType && a.RequiredCount <= currentCount)
            .ToListAsync();

        var existingIds = await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId)
            .ToListAsync();

        var toAward = achievements.Where(a => !existingIds.Contains(a.Id)).ToList();

        foreach (var ach in toAward)
        {
            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = ach.Id,
                EarnedAt = DateTime.UtcNow
            });
        }

        if (toAward.Any())
            await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Получить все достижения пользователя с данными о достижении
    /// </summary>
    public async Task<List<UserAchievement>> GetUserAchievementsAsync(int userId)
    {
        return await _context.UserAchievements
            .Include(ua => ua.Achievement)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.EarnedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить список всех возможных достижений (для отображения недоступных)
    /// </summary>
    public async Task<List<Achievement>> GetAllAchievementsAsync()
    {
        return await _context.Achievements.ToListAsync();
    }
}