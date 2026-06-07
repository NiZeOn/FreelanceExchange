namespace FreelanceExchange.API.DTOs;

public class UserRoleUpdateDto
{
    public int UserId { get; set; }
    public int NewRoleId { get; set; } // 1-5
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalTurnover { get; set; }
    public decimal TotalCommission { get; set; } // если комиссия есть
}