namespace WebAPI.Models;

public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
}