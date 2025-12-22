using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PiggyzenMvp.API.DTOs;
using PiggyzenMvp.API.Services.Config;

namespace PiggyzenMvp.API.Services;

public class TransactionImportService
{
    private const int MaxLayoutSampleRows = 200;
    private const int MaxSchemaSampleRows = 50;
    private const int MinLayoutColumnCount = 3;
    private const string LayoutIgnoreReason = "Felaktigt antal kolumner";

    private readonly NormalizeService _normalizeService;
    private readonly EffectiveImportConfig _importConfig;
    private readonly string[] _dateFormats;

    public TransactionImportService(NormalizeService normalizeService, EffectiveImportConfig importConfig)
    {
        _normalizeService = normalizeService;
        _importConfig = importConfig;
        _dateFormats = importConfig.DateFormats.ToArray();
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
        var layoutIgnoredRows = BuildIgnoredRows(safeContext.IgnoredRows, LayoutIgnoreReason);

        if (
            !TryBuildSchema(
                safeContext.ValidRows,
                safeContext.ColumnCount,
                out var schema,
                out var dataRows,
                out var schemaConfidence,
                out var headerDetected,
                out var headerRow,
                out var schemaError
            )
        )
        {
            var fallbackColumns = BuildColumnHints(null, headerRow, safeContext.ColumnCount);
            preview = new TransactionImportPreviewResult
            {
                Separator = safeContext.Separator,
                ColumnCount = safeContext.ColumnCount,
                LayoutConfidence = safeContext.LayoutConfidence,
                Rows = BuildPreviewRows(safeContext.ValidRows, previewRowCount, headerRow),
                LayoutIgnoredRows = layoutIgnoredRows,
                Columns = fallbackColumns,
                HeaderRow = headerDetected && headerRow != null
                    ? new TransactionImportPreviewHeader
                    {
                        LineNumber = headerRow.LineNumber,
                        Columns = headerRow.Columns.ToArray(),
                    }
                    : null,
            };

            preview.ParsingErrors.Add(schemaError ?? "Kunde inte avgöra kolumnerna.");
            return preview;
        }

        var columnHints = BuildColumnHints(
            schema,
            headerDetected && headerRow != null ? headerRow : null,
            safeContext.ColumnCount
        );
        var (previewEligibleRows, previewIgnoredRows) = BuildPreviewEligibleRows(
            dataRows,
            schema
        );
        var previewRows = BuildPreviewRows(previewEligibleRows, previewRowCount, headerRow);

        preview = new TransactionImportPreviewResult
        {
            Separator = safeContext.Separator,
            ColumnCount = safeContext.ColumnCount,
            LayoutConfidence = safeContext.LayoutConfidence,
            SchemaConfidence = schemaConfidence,
            SuggestedSchema = BuildSchemaDefinition(schema),
            HeaderRow = headerDetected && headerRow != null
                ? new TransactionImportPreviewHeader
                {
                    LineNumber = headerRow.LineNumber,
                    Columns = headerRow.Columns.ToArray(),
                }
                : null,
            Rows = previewRows,
            Columns = columnHints,
            LayoutIgnoredRows = layoutIgnoredRows,
            PreviewIgnoredRows = previewIgnoredRows,
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
                out var schemaConfidence,
                out var headerDetected,
                out var headerRow,
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

        if (!TryDetectLayout(inputRows, out var layout, out var detectionError))
        {
            errors.Add(detectionError ?? "Kunde inte avgöra separatorn för importen.");
            return false;
        }

        FilterRows(layout.ParsedRows, layout.ColumnCount, out var validRows, out var ignoredRows);

        foreach (var row in validRows)
        {
            CleanRowCells(row);
        }

        context = new TransactionImportProcessingContext(
            layout.Separator,
            layout.ColumnCount,
            layout.Confidence,
            validRows,
            ignoredRows
        );

        return true;
    }

    private bool TryDetectLayout(
        List<InputRow> rows,
        out LayoutDetectionResult result,
        out string? error
    )
    {
        result = default!;
        error = null;

        if (!rows.Any())
        {
            error = "Ingen rad hittades att tolka.";
            return false;
        }

        var sampleRows = rows.Take(MaxLayoutSampleRows).ToList();
        if (!sampleRows.Any())
        {
            error = "Ingen användbar text hittades.";
            return false;
        }

        LayoutCandidate? bestCandidate = null;

        foreach (var candidate in _importConfig.CandidateSeparators)
        {
            var splits = sampleRows
                .Select(row => new RowData(row.LineNumber, row.Text, SplitRow(row.Text, candidate)))
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
            if (modeGroup.Key < MinLayoutColumnCount)
            {
                continue;
            }

            var score = modeGroup.Count();

            if (
                bestCandidate == null
                || score > bestCandidate.Score
                || (score == bestCandidate.Score && modeGroup.Key > bestCandidate.ColumnCount)
            )
            {
                bestCandidate = new LayoutCandidate(candidate, modeGroup.Key, score);
            }
        }

        if (bestCandidate == null)
        {
            error = "Kunde inte avgöra separatorn för importen.";
            return false;
        }

        var parsedRows = rows
            .Select(row => new RowData(row.LineNumber, row.Text, SplitRow(row.Text, bestCandidate.Separator)))
            .ToList();

        var sampleCount = sampleRows.Count;
        var confidence = sampleCount > 0
            ? Math.Min(1m, bestCandidate.Score / (decimal)sampleCount)
            : 0m;

        result = new LayoutDetectionResult(
            bestCandidate.Separator,
            bestCandidate.ColumnCount,
            confidence,
            parsedRows
        );

        return true;
    }

    private static void FilterRows(
        List<RowData> parsedRows,
        int expectedColumnCount,
        out List<RowData> validRows,
        out List<RowData> ignoredRows
    )
    {
        validRows = new List<RowData>();
        ignoredRows = new List<RowData>();

        foreach (var row in parsedRows)
        {
            if (row.Columns.Length == expectedColumnCount)
            {
                validRows.Add(row);
            }
            else
            {
                ignoredRows.Add(row);
            }
        }
    }

    private static string[] SplitRow(string text, char separator) => text.Split(separator);

    private bool TryBuildSchema(
        List<RowData> validRows,
        int expectedColumnCount,
        out TransactionImportSchema schema,
        out List<RowData> dataRows,
        out decimal schemaConfidence,
        out bool headerDetected,
        out RowData? headerRow,
        out string? error
    )
    {
        schema = default!;
        dataRows = new List<RowData>();
        schemaConfidence = 0m;
        headerDetected = false;
        headerRow = null;
        error = null;

        if (!validRows.Any())
        {
            error = "Ingen rad hittades att tolka.";
            return false;
        }

        var rowsForInference = validRows;

        if (
            TryDetectHeaderSchema(validRows, expectedColumnCount, out schema, out var headerIndex, out var detectedHeader)
        )
        {
            headerDetected = true;
            headerRow = detectedHeader;
            dataRows = validRows.Skip(headerIndex + 1).ToList();
            schemaConfidence = 1m;
            return true;
        }

        var headerLikeIndex = FindHeaderLikeRowIndex(validRows);
        if (headerLikeIndex.HasValue)
        {
            headerRow = validRows[headerLikeIndex.Value];
            rowsForInference = validRows.Skip(headerLikeIndex.Value + 1).ToList();
        }

        if (
            TryInferSchema(
                rowsForInference,
                expectedColumnCount,
                out schema,
                out var confidence,
                out var inferenceError
            )
        )
        {
            schemaConfidence = confidence;
            dataRows = rowsForInference;
            return true;
        }

        error = inferenceError;
        return false;
    }

    private bool TryBuildSchemaFromHeader(
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
            var headerKey = ImportNormalization.NormalizeHeader(headerRow.Columns[index]);
            if (_importConfig.HeaderAliasesNormalized.TryGetValue(headerKey, out var field)
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

    private bool TryInferSchema(
        List<RowData> validRows,
        int expectedColumnCount,
        out TransactionImportSchema schema,
        out decimal confidence,
        out string error
    )
    {
        schema = default!;
        confidence = 0m;
        error = string.Empty;

        if (expectedColumnCount <= 0)
        {
            error = "Kunde inte avgöra kolumnantalet.";
            return false;
        }

        var sampleRows = validRows.Take(MaxSchemaSampleRows).ToList();
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

                var normalized = ImportNormalization.NormalizeText(cell);
                foreach (var token in _importConfig.TypeIndicatorTokens)
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
            .Where(x => x.DateMatches >= minDateMatches)
            .OrderByDescending(x => x.DateMatches)
            .FirstOrDefault();

        if (dateCandidate == null)
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
            .Where(c => c.index != amountIdx)
            .OrderBy(c => c.NegativeMoneyCount)
            .ThenByDescending(c => c.MoneyMatches)
            .Select(c => (int?)c.index)
            .FirstOrDefault();

        var descriptionCandidate = metrics
            .Select((metric, index) => new
            {
                index,
                Score = ComputeTextScore(metric),
                AverageLength = metric.Samples == 0 ? 0 : metric.TotalLength / (double)metric.Samples,
            })
            .Where(x => x.index != transactionDateIdx && x.index != amountIdx)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (descriptionCandidate == null || descriptionCandidate.Score <= 0 || descriptionCandidate.AverageLength < 3)
        {
            error = "Kunde inte avgöra vilken kolumn som innehåller transaktionstexten.";
            return false;
        }

        var descriptionIdx = descriptionCandidate.index;

        var bookingDateIdx = metrics
            .Select((metric, index) => new { metric.DateMatches, index })
            .Where(x => x.index != transactionDateIdx && x.DateMatches > 0)
            .OrderByDescending(x => x.DateMatches)
            .Select(x => (int?)x.index)
            .FirstOrDefault();

        var typeCandidate = metrics
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
            .FirstOrDefault();

        schema = new TransactionImportSchema(
            transactionDateIdx,
            descriptionIdx,
            amountIdx,
            bookingDateIdx,
            typeCandidate?.index,
            balanceIdx,
            expectedColumnCount
        );

        var sampleCountDecimal = Math.Max(1, sampleCount);
        var dateConfidence = Math.Min(1m, dateCandidate.DateMatches / (decimal)sampleCountDecimal);
        var amountConfidence = Math.Min(1m, metrics[amountIdx].MoneyMatches / (decimal)sampleCountDecimal);
        var descriptionConfidence = Math.Min(
            1m,
            (decimal)Math.Min(1.0, descriptionCandidate.Score / 30.0)
        );

        var baseConfidence = (dateConfidence + amountConfidence + descriptionConfidence) / 3m;

        var typeConfidence = typeCandidate != null
            ? Math.Min(1m, typeCandidate.TypeKeywordMatches / (decimal)sampleCountDecimal)
            : 0m;

        confidence = Math.Min(1m, baseConfidence + typeConfidence * 0.25m);

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
            if (!TryValidateBasic(row, schema, out var validationReason))
            {
                result.ParsingErrors.Add(validationReason);
                continue;
            }

            TryParseDate(row.Columns[schema.TransactionDateIdx], out var transactionDate);
            var description = row.Columns[schema.DescriptionIdx];
            TryParseAmount(row.Columns[schema.AmountIdx], out var amount);

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

    private static List<TransactionImportPreviewRow> BuildPreviewRows(
        IEnumerable<RowData> rows,
        int previewRowCount,
        RowData? headerRow = null
    )
    {
        return rows
            .Where(row => !IsSameRow(row, headerRow))
            .Take(previewRowCount)
            .Select(
                row => new TransactionImportPreviewRow
                {
                    LineNumber = row.LineNumber,
                    RawRow = row.RawRow,
                    Columns = row.Columns.ToArray(),
                }
            )
            .ToList();
    }

    private static bool IsSameRow(RowData row, RowData? headerRow)
    {
        return headerRow != null && row.LineNumber == headerRow.LineNumber;
    }

    private static List<TransactionImportIgnoredRow> BuildIgnoredRows(
        IEnumerable<RowData> ignoredRows,
        string reason
    )
    {
        return ignoredRows
            .Select(row => BuildIgnoredRow(row, reason))
            .ToList();
    }

    private static TransactionImportIgnoredRow BuildIgnoredRow(RowData row, string reason)
    {
        return new TransactionImportIgnoredRow
        {
            LineNumber = row.LineNumber,
            RawRow = row.RawRow,
            ColumnCount = row.Columns.Length,
            Reason = reason,
        };
    }

    private static TransactionImportSchemaDefinition BuildSchemaDefinition(TransactionImportSchema schema)
    {
        return new TransactionImportSchemaDefinition
        {
            ColumnCount = schema.ColumnCount,
            TransactionDateIndex = schema.TransactionDateIdx,
            DescriptionIndex = schema.DescriptionIdx,
            AmountIndex = schema.AmountIdx,
            BookingDateIndex = schema.BookingDateIdx,
            TransactionKindIndex = schema.TypeIdx,
            BalanceIndex = schema.BalanceIdx,
        };
    }

    private static IReadOnlyList<TransactionImportPreviewColumn> BuildColumnHints(
        TransactionImportSchema? schema,
        RowData? headerRow,
        int columnCount
    )
    {
        var columns = new List<TransactionImportPreviewColumn>(columnCount);

        for (var index = 0; index < columnCount; index++)
        {
            var displayName = BuildColumnDisplayName(headerRow, index);
            columns.Add(
                new TransactionImportPreviewColumn
                {
                    Index = index,
                    DisplayName = displayName,
                    SuggestedField = MapColumnToField(schema, index),
                }
            );
        }

        return columns;
    }

    private (List<RowData> EligibleRows, List<TransactionImportIgnoredRow> IgnoredRows)
        BuildPreviewEligibleRows(
            List<RowData> dataRows,
            TransactionImportSchema schema
        )
    {
        var eligibleRows = new List<RowData>();
        var ignoredRows = new List<TransactionImportIgnoredRow>();

        foreach (var row in dataRows)
        {
            if (TryValidateBasic(row, schema, out var reason))
            {
                eligibleRows.Add(row);
            }
            else
            {
                ignoredRows.Add(BuildIgnoredRow(row, reason));
            }
        }

        return (eligibleRows, ignoredRows);
    }

    private bool TryDetectHeaderSchema(
        List<RowData> rows,
        int expectedColumnCount,
        out TransactionImportSchema schema,
        out int headerIndex,
        out RowData? headerRow
    )
    {
        schema = default!;
        headerIndex = -1;
        headerRow = null;

        for (var i = 0; i < rows.Count; i++)
        {
            if (TryBuildSchemaFromHeader(rows[i], expectedColumnCount, out schema))
            {
                headerIndex = i;
                headerRow = rows[i];
                return true;
            }
        }

        return false;
    }

    private int? FindHeaderLikeRowIndex(List<RowData> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (IsHeaderRowCandidate(rows[i]))
            {
                return i;
            }
        }

        return null;
    }

    private int? FindHeaderRowIndex(List<RowData> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (IsHeaderRowCandidate(rows[i]))
            {
                return i;
            }
        }

        return null;
    }

    private bool IsHeaderRowCandidate(RowData row)
    {
        if (row.Columns.Length == 0)
        {
            return false;
        }

        foreach (var column in row.Columns)
        {
            if (ContainsHeaderIndicator(column))
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsHeaderIndicator(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = ImportNormalization.NormalizeHeader(input);
        foreach (var token in _importConfig.HeaderIndicatorTokens)
        {
            if (normalized.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildColumnDisplayName(RowData? headerRow, int index)
    {
        if (
            headerRow != null
            && index >= 0
            && index < headerRow.Columns.Length
            && !string.IsNullOrWhiteSpace(headerRow.Columns[index])
        )
        {
            return headerRow.Columns[index];
        }

        return $"Kolumn {index}";
    }

    private static TransactionImportFieldType MapColumnToField(
        TransactionImportSchema? schema,
        int index
    )
    {
        if (schema == null)
        {
            return TransactionImportFieldType.Unknown;
        }

        if (schema.TransactionDateIdx == index)
        {
            return TransactionImportFieldType.TransactionDate;
        }

        if (schema.BookingDateIdx == index)
        {
            return TransactionImportFieldType.BookingDate;
        }

        if (schema.DescriptionIdx == index)
        {
            return TransactionImportFieldType.Description;
        }

        if (schema.AmountIdx == index)
        {
            return TransactionImportFieldType.Amount;
        }

        if (schema.BalanceIdx == index)
        {
            return TransactionImportFieldType.Balance;
        }

        if (schema.TypeIdx == index)
        {
            return TransactionImportFieldType.TransactionType;
        }

        return TransactionImportFieldType.Unknown;
    }

    private static double ComputeTextScore(ColumnMetrics metric)
    {
        if (metric.Samples == 0)
        {
            return 0;
        }

        var averageLength = metric.TotalLength / (double)metric.Samples;
        var dateFraction = Math.Min(1.0, (double)metric.DateMatches / metric.Samples);
        var moneyFraction = Math.Min(1.0, (double)metric.MoneyMatches / metric.Samples);
        var nonDate = 1.0 - dateFraction;
        var nonMoney = 1.0 - moneyFraction;

        return averageLength * nonDate * nonMoney;
    }

    private bool TryValidateBasic(RowData row, TransactionImportSchema schema, out string reason)
    {
        reason = string.Empty;

        if (row.Columns.Length < schema.ColumnCount)
        {
            reason =
                $"Rad {row.LineNumber}: Raden innehåller {row.Columns.Length} kolumner men {schema.ColumnCount} förväntades.";
            return false;
        }

        if (!TryParseDate(row.Columns[schema.TransactionDateIdx], out _))
        {
            reason =
                $"Rad {row.LineNumber}: Ogiltigt transaktionsdatum: \"{row.Columns[schema.TransactionDateIdx]}\"";
            return false;
        }

        var description = row.Columns[schema.DescriptionIdx];
        if (string.IsNullOrWhiteSpace(description))
        {
            reason = $"Rad {row.LineNumber}: Textbeskrivning saknas.";
            return false;
        }

        if (!TryParseAmount(row.Columns[schema.AmountIdx], out _))
        {
            reason =
                $"Rad {row.LineNumber}: Ogiltigt belopp: \"{row.Columns[schema.AmountIdx]}\"";
            return false;
        }

        return true;
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

    private bool TryParseDate(string input, out DateTime? date)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            date = null;
            return false;
        }

        var trimmed = input.Trim();

        var parsed = DateTime.TryParseExact(
            trimmed,
            _dateFormats,
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
        decimal LayoutConfidence,
        List<RowData> ValidRows,
        List<RowData> IgnoredRows
    );

    private sealed record LayoutDetectionResult(
        char Separator,
        int ColumnCount,
        decimal Confidence,
        List<RowData> ParsedRows
    );

    private sealed record LayoutCandidate(char Separator, int ColumnCount, int Score);

    private sealed class ColumnMetrics
    {
        public int Samples;
        public int DateMatches;
        public int MoneyMatches;
        public int NegativeMoneyCount;
        public int TypeKeywordMatches;
        public int TotalLength;
    }

}
