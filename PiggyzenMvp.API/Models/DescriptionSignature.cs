namespace PiggyzenMvp.API.Models
{
    public enum DescriptionSignatureSource
    {
        Auto = 0,
        Manual = 1,
    }

    public class DescriptionSignature
    {
        public int Id { get; set; }

        public string NormalizedDescription { get; set; } = string.Empty;
        public TransactionKind Kind { get; set; } = TransactionKind.Unknown;
        public bool IsPositive { get; set; }

        public bool IsMachineGenerated { get; set; }
        public decimal MachineConfidence { get; set; }

        public string? MerchantCandidate { get; set; }
        public string? Note { get; set; }

        public DescriptionSignatureSource MachineSource { get; set; } = DescriptionSignatureSource.Auto;

        public int SeenCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public string AlgorithmVersion { get; set; } = string.Empty;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
