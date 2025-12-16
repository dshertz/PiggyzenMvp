namespace PiggyzenMvp.Blazor.DTOs
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public DateTime? BookingDate { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal? Balance { get; set; }
        public string? Note { get; set; }

        // Kategori-info
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }

        // Huvudkategori (Type)
        public int? TypeId { get; set; }
        public string? TypeName { get; set; }
    }
}
