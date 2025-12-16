namespace PiggyzenMvp.API.DTOs;

public class TransactionListDto
{
    public int Id { get; set; }
    public DateTime? BookingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }

    public string? Note { get; set; }

    // Info om kategori
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    // Info om huvudkategori (Type)
    // Ändra till CategoryGroup
    public int? TypeId { get; set; }
    public string? TypeName { get; set; }
}
