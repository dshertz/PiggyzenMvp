namespace PiggyzenMvp.API.DTOs.Transactions;

public record TransactionMetadataUpdateDto(
    int TransactionId,
    string? Note,
    IReadOnlyCollection<string>? TagsSet,
    IReadOnlyCollection<string>? TagsAdd,
    IReadOnlyCollection<string>? TagsRemove
);
