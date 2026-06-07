namespace FreelanceExchange.API.Models;

public class UserAchievement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int AchievementId { get; set; }
    public Achievement Achievement { get; set; } = null!;
    public DateTime EarnedAt { get; set; }
}