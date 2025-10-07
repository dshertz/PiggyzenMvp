namespace PiggyzenMvp.API.DTOs;

public class TransactionImportDto
{
    public DateTime? BookingDate { get; set; }
    public required DateTime TransactionDate { get; set; }
    public required string Description { get; set; }
    public required decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public required string ImportId { get; set; }
    public int SourceLineNumber { get; set; }
}
