namespace PiggyzenMvp.API.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public DateTime? BookingDate { get; set; }
        public required DateTime TransactionDate { get; set; }
        public required string Description { get; set; }
        public required string NormalizedDescription { get; set; }
        public required decimal Amount { get; set; }
        public decimal? Balance { get; set; }
        public string? TypeRaw { get; set; }
        public TransactionKind Kind { get; set; } = TransactionKind.Unknown;
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public int? DescriptionSignatureId { get; set; }
        public DescriptionSignature? DescriptionSignature { get; set; }
        public required string ImportId { get; set; }
        public required DateTime ImportedAtUtc { get; set; }
        public required int ImportSequence { get; set; }
        public string? RawRow { get; set; }
        public CategorizationUsage? CategorizationUsage { get; set; }
        public string? Note { get; set; }
        public List<TransactionTag> Tags { get; set; } = new();
    }
}
