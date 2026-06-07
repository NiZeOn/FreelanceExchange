namespace FreelanceExchange.API.DTOs;

public class CreateResponseDto
{
    public int OrderId { get; set; }
    public decimal ProposedPrice { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
}

public class ResponseDto
{
    public int Id { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public decimal ProposedPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FreelancerId { get; set; }
    public string FreelancerName { get; set; } = string.Empty;
    public string? FreelancerAvatarUrl { get; set; }  // добавлено поле для аватара
    public int OrderId { get; set; }
    public string OrderTitle { get; set; } = string.Empty;
}