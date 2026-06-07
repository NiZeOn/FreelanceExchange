namespace FreelanceExchange.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public bool IsBlocked { get; set; } = false;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    
    public string? Skills { get; set; }
    public decimal? HourlyRate { get; set; }
    public string? Bio { get; set; }
}