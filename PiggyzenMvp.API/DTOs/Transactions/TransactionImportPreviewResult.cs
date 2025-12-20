using System;
using System.Collections.Generic;

namespace PiggyzenMvp.API.DTOs;

public class TransactionImportPreviewResult
{
    public char Separator { get; init; }
    public int ColumnCount { get; init; }
    public IReadOnlyList<TransactionImportPreviewRow> Rows { get; init; } =
        Array.Empty<TransactionImportPreviewRow>();
    public IReadOnlyList<TransactionImportIgnoredRow> IgnoredRows { get; init; } =
        Array.Empty<TransactionImportIgnoredRow>();
    public List<string> ParsingErrors { get; } = new();
}

public class TransactionImportPreviewRow
{
    public int LineNumber { get; init; }
    public string RawRow { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
}

public class TransactionImportIgnoredRow
{
    public int LineNumber { get; init; }
    public string RawRow { get; init; } = string.Empty;
    public int ColumnCount { get; init; }
}
