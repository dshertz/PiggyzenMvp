using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PiggyzenMvp.API.DTOs;

namespace PiggyzenMvp.API.Services;

public class TransactionImportService
{
    private static readonly char[] CandidateSeparators = new[] { '\t', ';', ',' };

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "MM-dd-yyyy",
        "MM/dd/yyyy",
    };

    private static readonly IReadOnlyDictionary<string, HeaderField> HeaderAliases =
        new Dictionary<string, HeaderField>(StringComparer.OrdinalIgnoreCase)
        {
            ["transaktionsdatum"] = HeaderField.TransactionDate,
            ["transactiondatum"] = HeaderField.TransactionDate,
            ["transactiondate"] = HeaderField.TransactionDate,
            ["bokforingsdatum"] = HeaderField.BookingDate,
            ["bookingdate"] = HeaderField.BookingDate,
            ["meddelande"] = HeaderField.Description,
            ["meddelandetext"] = HeaderField.Description,
            ["text"] = HeaderField.Description,
            ["textmeddelande"] = HeaderField.Description,
            ["beskrivning"] = HeaderField.Description,
            ["referens"] = HeaderField.Description,
            ["motpart"] = HeaderField.Description,
            ["transaktionstyp"] = HeaderField.Type,
            ["transactiontype"] = HeaderField.Type,
            ["typ"] = HeaderField.Type,
            ["type"] = HeaderField.Type,
            ["belopp"] = HeaderField.Amount,
            ["insattning"] = HeaderField.Amount,
            ["uttag"] = HeaderField.Amount,
            ["amount"] = HeaderField.Amount,
            ["insattninguttag"] = HeaderField.Amount,
            ["saldo"] = HeaderField.Balance,
            ["behallning"] = HeaderField.Balance,
            ["balans"] = HeaderField.Balance,
            ["balance"] = HeaderField.Balance,
        };

    private static readonly string[] TypeIndicatorTokens =
    {
        "swish",
        "kort",
        "card",
        "autogiro",
        "betal",
        "avgift",
        "overforing",
        "transfer",
        "insattning",
        "kontant",
        "deposit",
        "loan",
        "lan",
        "amort",
        "ranta",
        "rabatt",
    };

    private readonly NormalizeService _normalizeService;

    public TransactionImportService(NormalizeService normalizeService)
    {
        _normalizeService = normalizeService;
    }

    public TransactionImportPreviewResult PreparePreview(string rawText, int previewRowCount = 5)
    {
        var preview = new TransactionImportPreviewResult();

        if (!TryCreateProcessingContext(rawText, out var context, out var errors))
        {
            preview.ParsingErrors.AddRange(errors);
            return preview;
        }

        var safeContext = context!;

        var rows = safeContext.ValidRows
            .Take(previewRowCount)
            .Select(row => new TransactionImportPreviewRow
            {
                LineNumber = row.LineNumber,
                RawRow = row.RawRow,
                Columns = row.Columns.ToArray(),
            })
            .ToList();

        preview = new TransactionImportPreviewResult
        {
            Separator = safeContext.Separator,
            ColumnCount = safeContext.ColumnCount,
            Rows = rows,
            IgnoredRows = safeContext.IgnoredRows
                .Select(row => new TransactionImportIgnoredRow
                {
                    LineNumber = row.LineNumber,
                    RawRow = row.RawRow,
                    ColumnCount = row.Columns.Length,
                })
                .ToList(),
        };

        return preview;
    }

    public TransactionImportParseResult ParseWithSchema(
        string rawText,
        TransactionImportSchemaDefinition schema
    )
    {
        var result = new TransactionImportParseResult();

        if (schema == null)
        {
            result.ParsingErrors.Add("Schema saknas.");
            return result;
        }

        if (!TryCreateProcessingContext(rawText, out var context, out var errors))
        {
            result.ParsingErrors.AddRange(errors);
            return result;
        }

        var safeContext = context!;

        if (schema.ColumnCount != safeContext.ColumnCount)
        {
            result.ParsingErrors.Add("Schema innehåller inte samma antal kolumner som importen.");
            return result;
        }

        if (!schema.TryValidate(safeContext.ColumnCount, out var schemaErrors))
        {
            result.ParsingErrors.AddRange(schemaErrors);
            return result;
        }

        var manualSchema = new TransactionImportSchema(
            schema.TransactionDateIndex,
            schema.DescriptionIndex,
            schema.AmountIndex,
            schema.BookingDateIndex,
            schema.TransactionKindIndex,
            schema.BalanceIndex,
            safeContext.ColumnCount
        );

        ParseRows(safeContext.ValidRows, manualSchema, result);

        if (!result.Transactions.Any() && !result.ParsingErrors.Any())
        {
            result.ParsingErrors.Add("Inga giltiga rader hittades.");
        }

        return result;
    }

    public TransactionImportParseResult ParseRawInput(string rawText)
    {
        var result = new TransactionImportParseResult();

        if (!TryCreateProcessingContext(rawText, out var context, out var errors))
        {
            result.ParsingErrors.AddRange(errors);
            return result;
        }

        var safeContext = context!;

        if (
            !TryBuildSchema(
                safeContext.ValidRows,
                safeContext.ColumnCount,
                out var schema,
                out var dataRows,
                out var schemaError
            )
        )
        {
            result.ParsingErrors.Add(schemaError ?? "Kunde inte avgöra kolumnerna.");
            return result;
        }
        ParseRows(dataRows, schema, result);

        if (!result.Transactions.Any() && !result.ParsingErrors.Any())
        {
            result.ParsingErrors.Add("Inga giltiga rader hittades.");
        }

        return result;
    }

    private string NormalizeLineEndings(string input)
    {
        return input.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

    private static List<InputRow> SplitIntoRows(string normalized)
    {
        return normalized
            .Split('\n')
            .Select(row => row.Trim())
            .Where(row => !string.IsNullOrWhiteSpace(row))
            .Select((value, index) => new InputRow(value, index + 1))
            .ToList();
    }

    private bool TryCreateProcessingContext(
        string rawText,
        out TransactionImportProcessingContext? context,
        out List<string> errors
    )
    {
        context = null;
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rawText))
        {
            errors.Add("Ingen text skickades in för import.");
            return false;
        }

        var normalized = NormalizeLineEndings(rawText);
        var inputRows = SplitIntoRows(normalized);

        if (!inputRows.Any())
        {
            errors.Add("Ingen användbar text hittades.");
            return false;
        }

        if (!TryDetectSeparator(inputRows, out var separator, out var expectedColumnCount, out var parsedRows))
        {
            errors.Add("Kunde inte avgöra separatorn för importen.");
            return false;
        }

        var validRows = parsedRows
            .Where(row => row.Columns.Length == expectedColumnCount)
            .ToList();

        var ignoredRows = parsedRows
            .Where(row => row.Columns.Length != expectedColumnCount)
            .ToList();

        foreach (var row in validRows)
        {
            CleanRowCells(row);
        }

        context = new TransactionImportProcessingContext(
            separator,
            expectedColumnCount,
            validRows,
            ignoredRows
        );

        return true;
    }

    private static bool TryDetectSeparator(
        List<InputRow> rows,
        out char separator,
        out int expectedColumnCount,
        out List<RowData> parsedRows
    )
    {
        parsedRows = new List<RowData>();
        expectedColumnCount = 0;
        separator = default;
        var bestScore = -1;
        var bestColumnCount = 0;
        var bestSeparatorRows = new List<RowData>();
        var bestSeparator = default(char);

        foreach (var candidate in CandidateSeparators)
        {
            var splits = rows
                .Select(
                    row =>
                        new RowData(
                            row.LineNumber,
                            row.Text,
                            row.Text.Split(candidate).Select(cell => cell.Trim()).ToArray()
                        )
                )
                .ToList();

            var columnGroups = splits
                .GroupBy(r => r.Columns.Length)
                .OrderByDescending(g => g.Count())
                .ToList();

            if (!columnGroups.Any())
            {
                continue;
            }

            var modeGroup = columnGroups.First();
            if (modeGroup.Key < 3)
            {
                continue;
            }

            var score = modeGroup.Count();

            if (score > bestScore || (score == bestScore && modeGroup.Key > bestColumnCount))
            {
                bestScore = score;
                bestColumnCount = modeGroup.Key;
                bestSeparatorRows = splits;
                bestSeparator = candidate;
            }
        }

        if (bestScore <= 0 || bestSeparatorRows.Count == 0)
        {
            return false;
        }

        separator = bestSeparator;
        expectedColumnCount = bestColumnCount;
        parsedRows = bestSeparatorRows;
        return true;
    }

    private static bool TryBuildSchema(
        List<RowData> parsedRows,
        int expectedColumnCount,
        out TransactionImportSchema schema,
        out List<RowData> dataRows,
        out string? error
    )
    {
        schema = default!;
        dataRows = new List<RowData>();
        error = null;

        if (!parsedRows.Any())
        {
            error = "Ingen rad hittades att tolka.";
            return false;
        }

        if (TryBuildSchemaFromHeader(parsedRows[0], expectedColumnCount, out schema))
        {
            dataRows = parsedRows.Skip(1).ToList();
            return true;
        }

        if (TryInferSchema(parsedRows, expectedColumnCount, out schema, out var inferenceError))
        {
            dataRows = parsedRows;
            return true;
        }

        error = inferenceError;
        return false;
    }

    private static bool TryBuildSchemaFromHeader(
        RowData headerRow,
        int expectedColumnCount,
        out TransactionImportSchema schema
    )
    {
        schema = default!;
        if (headerRow.Columns.Length == 0)
            return false;

        var headerMap = new Dictionary<HeaderField, int>();

        for (var index = 0; index < headerRow.Columns.Length; index++)
        {
            var headerKey = NormalizeHeaderName(headerRow.Columns[index]);
            if (HeaderAliases.TryGetValue(headerKey, out var field)
                && !headerMap.ContainsKey(field))
            {
                headerMap[field] = index;
            }
        }

        if (!headerMap.TryGetValue(HeaderField.TransactionDate, out var transactionDateIdx))
        {
            if (!headerMap.TryGetValue(HeaderField.BookingDate, out var altIdx))
            {
                return false;
            }

            transactionDateIdx = altIdx;
        }

        if (!headerMap.TryGetValue(HeaderField.Description, out var descriptionIdx))
            return false;

        if (!headerMap.TryGetValue(HeaderField.Amount, out var amountIdx))
            return false;

        var hasBooking = headerMap.TryGetValue(HeaderField.BookingDate, out var bookingDateIdx);
        var hasType = headerMap.TryGetValue(HeaderField.Type, out var typeIdx);
        var hasBalance = headerMap.TryGetValue(HeaderField.Balance, out var balanceIdx);

        schema = new TransactionImportSchema(
            transactionDateIdx,
            descriptionIdx,
            amountIdx,
            hasBooking && bookingDateIdx != transactionDateIdx ? bookingDateIdx : null,
            hasType ? typeIdx : null,
            hasBalance && balanceIdx != amountIdx ? balanceIdx : null,
            expectedColumnCount
        );

        return true;
    }

    private static string NormalizeHeaderName(string input)
    {
        var normalized = input.ToLowerInvariant();
        normalized = normalized.Replace("å", "a").Replace("ä", "a").Replace("ö", "o");
        var builder = new StringBuilder();

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static bool TryInferSchema(
        List<RowData> parsedRows,
        int expectedColumnCount,
        out TransactionImportSchema schema,
        out string error
    )
    {
        schema = default!;
        error = string.Empty;

        if (expectedColumnCount <= 0)
        {
            error = "Kunde inte avgöra kolumnantalet.";
            return false;
        }

        var sampleRows = parsedRows.Take(20).ToList();
        if (!sampleRows.Any())
        {
            error = "Det finns inga rader att analysera.";
            return false;
        }

        var metrics = Enumerable.Range(0, expectedColumnCount)
            .Select(_ => new ColumnMetrics())
            .ToArray();

        foreach (var row in sampleRows)
        {
            for (var columnIndex = 0; columnIndex < expectedColumnCount; columnIndex++)
            {
                if (columnIndex >= row.Columns.Length)
                {
                    continue;
                }

                var cell = row.Columns[columnIndex];
                metrics[columnIndex].Samples++;
                metrics[columnIndex].TotalLength += cell.Length;

                if (TryParseDate(cell, out _))
                {
                    metrics[columnIndex].DateMatches++;
                }

                if (TryParseAmount(cell, out var amount))
                {
                    metrics[columnIndex].MoneyMatches++;
                    if (amount < 0)
                    {
                        metrics[columnIndex].NegativeMoneyCount++;
                    }
                }

                var normalized = NormalizeForSchemaValue(cell);
                foreach (var token in TypeIndicatorTokens)
                {
                    if (normalized.Contains(token))
                    {
                        metrics[columnIndex].TypeKeywordMatches++;
                        break;
                    }
                }
            }
        }

        var sampleCount = sampleRows.Count;
        var minDateMatches = Math.Max(1, sampleCount / 4);

        var dateCandidate = metrics
            .Select((metric, index) => new { metric.DateMatches, index })
            .Where(x => x.DateMatches > 0)
            .OrderByDescending(x => x.DateMatches)
            .FirstOrDefault();

        if (dateCandidate == null || dateCandidate.DateMatches < minDateMatches)
        {
            error = "Kunde inte avgöra vilken kolumn som innehåller transaktionsdatum.";
            return false;
        }

        var transactionDateIdx = dateCandidate.index;

        var moneyCandidates = metrics
            .Select((metric, index) => new
            {
                index,
                metric.MoneyMatches,
                metric.NegativeMoneyCount
            })
            .Where(x => x.MoneyMatches > 0)
            .OrderByDescending(x => x.NegativeMoneyCount)
            .ThenByDescending(x => x.MoneyMatches)
            .ToList();

        if (!moneyCandidates.Any())
        {
            error = "Kunde inte avgöra vilken kolumn som innehåller belopp.";
            return false;
        }

        var amountIdx = moneyCandidates.First().index;
        int? balanceIdx = moneyCandidates
            .FirstOrDefault(c => c.index != amountIdx)?.index;

        var descriptionCandidate = metrics
            .Select((metric, index) => new
            {
                index,
                AverageLength = metric.Samples == 0 ? 0 : metric.TotalLength / (double)metric.Samples
            })
            .Where(x => x.index != transactionDateIdx && x.index != amountIdx)
            .OrderByDescending(x => x.AverageLength)
            .FirstOrDefault();

        if (descriptionCandidate == null || descriptionCandidate.AverageLength < 3)
        {
            error = "Kunde inte avgöra vilken kolumn som innehåller transaktionstexten.";
            return false;
        }

        var descriptionIdx = descriptionCandidate.index;

        int? bookingDateIdx = metrics
            .Select((metric, index) => new { metric.DateMatches, index })
            .Where(x => x.index != transactionDateIdx && x.DateMatches > 0)
            .OrderByDescending(x => x.DateMatches)
            .Select(x => (int?)x.index)
            .FirstOrDefault();

        int? typeIdx = metrics
            .Select((metric, index) => new
            {
                index,
                metric.TypeKeywordMatches,
                AverageLength = metric.Samples == 0 ? 0 : metric.TotalLength / (double)metric.Samples
            })
            .Where(
                x =>
                    x.index != amountIdx
                    && x.index != transactionDateIdx
                    && x.index != descriptionIdx
                    && x.TypeKeywordMatches > 0
            )
            .OrderByDescending(x => x.TypeKeywordMatches)
            .ThenBy(x => x.AverageLength)
            .Select(x => (int?)x.index)
            .FirstOrDefault();

        schema = new TransactionImportSchema(
            transactionDateIdx,
            descriptionIdx,
            amountIdx,
            bookingDateIdx,
            typeIdx,
            balanceIdx,
            expectedColumnCount
        );

        return true;
    }

    private void ParseRows(
        List<RowData> rows,
        TransactionImportSchema schema,
        TransactionImportParseResult result
    )
    {
        foreach (var row in rows)
        {
            if (row.Columns.Length < schema.ColumnCount)
            {
                result.ParsingErrors.Add(
                    $"Rad {row.LineNumber}: Raden innehåller {row.Columns.Length} kolumner men {schema.ColumnCount} förväntades."
                );
                continue;
            }

            if (!TryParseDate(row.Columns[schema.TransactionDateIdx], out var transactionDate))
            {
                result.ParsingErrors.Add(
                    $"Rad {row.LineNumber}: Ogiltigt transaktionsdatum: \"{row.Columns[schema.TransactionDateIdx]}\""
                );
                continue;
            }

            var description = row.Columns[schema.DescriptionIdx];
            if (string.IsNullOrWhiteSpace(description))
            {
                result.ParsingErrors.Add($"Rad {row.LineNumber}: Textbeskrivning saknas.");
                continue;
            }

            if (!TryParseAmount(row.Columns[schema.AmountIdx], out var amount))
            {
                result.ParsingErrors.Add(
                    $"Rad {row.LineNumber}: Ogiltigt belopp: \"{row.Columns[schema.AmountIdx]}\""
                );
                continue;
            }

            decimal? balance = null;
            if (schema.BalanceIdx.HasValue)
            {
                if (TryParseAmount(row.Columns[schema.BalanceIdx.Value], out var parsedBalance))
                {
                    balance = parsedBalance;
                }
                else if (!string.IsNullOrWhiteSpace(row.Columns[schema.BalanceIdx.Value]))
                {
                    result.ParsingErrors.Add(
                        $"Rad {row.LineNumber}: Ogiltigt saldo: \"{row.Columns[schema.BalanceIdx.Value]}\""
                    );
                    continue;
                }
            }

            DateTime? bookingDate = null;
            if (schema.BookingDateIdx.HasValue)
            {
                var bookingValue = row.Columns[schema.BookingDateIdx.Value];
                if (!string.IsNullOrWhiteSpace(bookingValue))
                {
                    if (TryParseDate(bookingValue, out var parsedBooking))
                    {
                        bookingDate = parsedBooking;
                    }
                    else
                    {
                        result.ParsingErrors.Add(
                            $"Rad {row.LineNumber}: Ogiltigt bokföringsdatum: \"{bookingValue}\""
                        );
                        continue;
                    }
                }
            }

            var typeRaw = schema.TypeIdx.HasValue ? row.Columns[schema.TypeIdx.Value] : null;

            result.Transactions.Add(
                new TransactionImportDto
                {
                    BookingDate = bookingDate,
                    TransactionDate = transactionDate!.Value,
                    Description = description,
                    NormalizedDescription = _normalizeService.Normalize(description),
                    Amount = amount!.Value,
                    Balance = balance,
                    TypeRaw = string.IsNullOrWhiteSpace(typeRaw) ? null : typeRaw,
                    ImportId = string.Empty,
                    SourceLineNumber = row.LineNumber,
                    RawRow = row.RawRow,
                }
            );
        }
    }

    private static void CleanRowCells(RowData row)
    {
        for (var i = 0; i < row.Columns.Length; i++)
        {
            row.Columns[i] = CleanCell(row.Columns[i]);
        }
    }

    private static string CleanCell(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            return string.Empty;
        }

        var cleaned = cell.Trim();

        while (cleaned.Length >= 2 && cleaned[0] == '"' && cleaned[^1] == '"')
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
        }

        return cleaned;
    }

    private static string NormalizeForSchemaValue(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().ToLowerInvariant();
        normalized = normalized.Replace("å", "a").Replace("ä", "a").Replace("ö", "o");
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            builder.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return builder.ToString();
    }

    private static bool TryParseDate(string input, out DateTime? date)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            date = null;
            return false;
        }

        var trimmed = input.Trim();

        var parsed = DateTime.TryParseExact(
            trimmed,
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate
        );

        if (parsed)
        {
            date = parsedDate;
            return true;
        }

        date = null;
        return false;
    }

    private static bool TryParseAmount(string input, out decimal? amount)
    {
        amount = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var normalized = cleaned.Replace(",", ".");

        if (decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed
        ))
        {
            amount = parsed;
            return true;
        }

        return false;
    }

    private sealed record InputRow(string Text, int LineNumber);

    private sealed record RowData(int LineNumber, string RawRow, string[] Columns);

    private sealed record TransactionImportSchema(
        int TransactionDateIdx,
        int DescriptionIdx,
        int AmountIdx,
        int? BookingDateIdx,
        int? TypeIdx,
        int? BalanceIdx,
        int ColumnCount
    );

    private sealed record TransactionImportProcessingContext(
        char Separator,
        int ColumnCount,
        List<RowData> ValidRows,
        List<RowData> IgnoredRows
    );

    private sealed class ColumnMetrics
    {
        public int Samples;
        public int DateMatches;
        public int MoneyMatches;
        public int NegativeMoneyCount;
        public int TypeKeywordMatches;
        public int TotalLength;
    }

    private enum HeaderField
    {
        TransactionDate,
        BookingDate,
        Description,
        Type,
        Amount,
        Balance,
    }
}
