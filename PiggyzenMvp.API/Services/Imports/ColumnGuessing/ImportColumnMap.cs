using System.Collections.Generic;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public sealed record ImportColumnMap(
    int? BookingDateIndex,
    int? TransactionDateIndex,
    int? TransactionTypeIndex,
    int? DescriptionIndex,
    int? AmountIndex,
    int? BalanceIndex,
    decimal TotalScore,
    decimal DateScore,
    decimal TransactionTypeScore,
    decimal DescriptionScore,
    decimal AmountScore,
    decimal BalanceScore,
    IReadOnlyList<int> RedundantColumns
);
