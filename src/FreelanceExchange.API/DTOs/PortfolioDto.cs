namespace FreelanceExchange.API.DTOs;

public class CreatePortfolioDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? ImageUrl { get; set; }
}

public class PortfolioDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}