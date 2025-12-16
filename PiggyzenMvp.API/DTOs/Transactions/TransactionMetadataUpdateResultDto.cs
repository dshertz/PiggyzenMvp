namespace PiggyzenMvp.API.DTOs.Transactions;

public record TransactionMetadataUpdateResultDto(
    int TransactionId,
    bool Updated,
    string? ErrorMessage
);
