namespace PiggyzenMvp.API.DTOs;

public class TransactionDto
{
    public DateTime? BookingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? CategoryName { get; set; }
}
