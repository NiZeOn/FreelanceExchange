public class CreateDisputeDto
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DisputeResponseDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderTitle { get; set; } = string.Empty;
    public int InitiatorId { get; set; }
    public string InitiatorName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public string? Resolution { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ModeratorId { get; set; }
    public string? ModeratorName { get; set; }
}

public class ResolveDisputeDto
{
    public string Resolution { get; set; } = string.Empty; // "Customer" или "Freelancer"
}