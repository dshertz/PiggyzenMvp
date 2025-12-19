using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.DTOs.DescriptionSignatures;

public class DescriptionSignatureUpdateDto
{
    public TransactionKind? Kind { get; set; }
    public bool? IsMachineGenerated { get; set; }
    public decimal? MachineConfidence { get; set; }
    public string? MerchantCandidate { get; set; }
    public string? Note { get; set; }
    public DescriptionSignatureSource? MachineSource { get; set; }
}
