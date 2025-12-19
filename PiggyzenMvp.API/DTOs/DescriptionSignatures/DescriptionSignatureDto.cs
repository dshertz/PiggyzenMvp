using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.DTOs.DescriptionSignatures;

public class DescriptionSignatureDto
{
    public int Id { get; set; }
    public string NormalizedDescription { get; set; } = string.Empty;
    public TransactionKind Kind { get; set; }
    public bool IsPositive { get; set; }
    public bool IsMachineGenerated { get; set; }
    public decimal MachineConfidence { get; set; }
    public string? MerchantCandidate { get; set; }
    public string? Note { get; set; }
    public DescriptionSignatureSource MachineSource { get; set; }
    public int SeenCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
}
