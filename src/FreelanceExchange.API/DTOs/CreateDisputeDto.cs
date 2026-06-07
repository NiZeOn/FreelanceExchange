namespace FreelanceExchange.API.DTOs;

public class CreateDisputeDto
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}