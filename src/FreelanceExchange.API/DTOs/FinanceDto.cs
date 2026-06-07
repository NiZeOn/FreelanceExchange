namespace FreelanceExchange.API.DTOs;

public class DepositDto
{
    public decimal Amount { get; set; }
}

public class WithdrawDto
{
    public decimal Amount { get; set; }
    public string PaymentDetails { get; set; } = string.Empty;
}

public class AccountDto
{
    public decimal Balance { get; set; }
    public decimal Blocked { get; set; }
}