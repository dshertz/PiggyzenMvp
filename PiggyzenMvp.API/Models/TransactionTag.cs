namespace PiggyzenMvp.API.Models
{
    public class TransactionTag
    {
        public int TransactionId { get; set; }
        public Transaction Transaction { get; set; } = null!;
        public string Value { get; set; } = null!;
    }
}
