namespace PiggyzenMvp.API.DTOs.Transactions;

public record SimilarTransactionDto(
    int Id,
    DateTime TransactionDate,
    string Description,
    decimal Amount
);
