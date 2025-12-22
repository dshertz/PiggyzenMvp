using System;
using System.Collections.Generic;

namespace PiggyzenMvp.API.DTOs;

public class TransactionImportPreviewResult
{
    public char Separator { get; init; }
    public int ColumnCount { get; init; }
    public decimal LayoutConfidence { get; init; }
    public decimal SchemaConfidence { get; init; }
    public TransactionImportSchemaDefinition? SuggestedSchema { get; init; }
    public TransactionImportPreviewHeader? HeaderRow { get; init; }
    public IReadOnlyList<TransactionImportPreviewColumn> Columns { get; init; } =
        Array.Empty<TransactionImportPreviewColumn>();
    public IReadOnlyList<TransactionImportFieldOption> FieldOptions { get; init; } =
        TransactionImportFieldOption.DefaultFieldOptions;
    public IReadOnlyList<TransactionImportPreviewRow> Rows { get; init; } =
        Array.Empty<TransactionImportPreviewRow>();
    public IReadOnlyList<TransactionImportIgnoredRow> LayoutIgnoredRows { get; init; } =
        Array.Empty<TransactionImportIgnoredRow>();
    public IReadOnlyList<TransactionImportIgnoredRow> PreviewIgnoredRows { get; init; } =
        Array.Empty<TransactionImportIgnoredRow>();
    public List<string> ParsingErrors { get; } = new();
}

public class TransactionImportPreviewColumn
{
    public int Index { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public TransactionImportFieldType SuggestedField { get; init; }
}

public class TransactionImportFieldOption
{
    public static readonly IReadOnlyList<TransactionImportFieldOption> DefaultFieldOptions =
        new[]
        {
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.Unknown,
                DisplayName = "Ingen",
            },
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.TransactionDate,
                DisplayName = "TransactionDate",
            },
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.BookingDate,
                DisplayName = "BookingDate",
            },
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.Description,
                DisplayName = "Description",
            },
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.Amount,
                DisplayName = "Amount",
            },
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.Balance,
                DisplayName = "Balance",
            },
            new TransactionImportFieldOption
            {
                FieldType = TransactionImportFieldType.TransactionType,
                DisplayName = "TransactionType",
            },
        };

    public TransactionImportFieldType FieldType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public enum TransactionImportFieldType
{
    Unknown,
    TransactionDate,
    BookingDate,
    Description,
    Amount,
    Balance,
    TransactionType,
}

public class TransactionImportPreviewRow
{
    public int LineNumber { get; init; }
    public string RawRow { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public TransactionImportPreviewRowStatus Status { get; init; } = TransactionImportPreviewRowStatus.Normal;
    public string? IgnoredReason { get; init; }
}

public class TransactionImportIgnoredRow
{
    public int LineNumber { get; init; }
    public string RawRow { get; init; } = string.Empty;
    public int ColumnCount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class TransactionImportPreviewHeader
{
    public int LineNumber { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
}

public enum TransactionImportPreviewRowStatus
{
    Normal,
    Invalid,
    LayoutIgnored,
}
