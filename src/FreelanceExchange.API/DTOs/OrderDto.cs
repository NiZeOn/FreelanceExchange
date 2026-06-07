namespace FreelanceExchange.API.DTOs;

public class CreateOrderDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public DateTime Deadline { get; set; }
    public int CategoryId { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? FreelancerId { get; set; }
    public string? FreelancerName { get; set; }
    public string? FreelancerFileUrl { get; set; }   // файл от фрилансера
    public string? CustomerFileUrl { get; set; }     // файл от заказчика
}

public class OrderFilterDto
{
    public string? Status { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public int? DeadlineDays { get; set; }
}