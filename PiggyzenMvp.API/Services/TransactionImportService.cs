using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PiggyzenMvp.API.DTOs;
using PiggyzenMvp.API.Services.Imports;
using PiggyzenMvp.API.Services.Imports.ColumnGuessing;
using ResolvedImportConfig = PiggyzenMvp.API.Services.Imports.ImportConfigService.ResolvedImportConfig;

namespace PiggyzenMvp.API.Services;

public class TransactionImportService
{
    private const int MaxLayoutSampleRows = 200;
    private const int MaxSchemaSampleRows = 50;
    private const int MinLayoutColumnCount = 3;
    private readonly NormalizeService _normalizeService;
    private readonly ResolvedImportConfig _importConfig;
    private readonly string[] _dateFormats;
    private readonly ImportColumnGuesser _columnGuesser;

    public TransactionImportService(
        NormalizeService normalizeService,
        ResolvedImportConfig importConfig,
        ImportColumnGuesser columnGuesser)
    {
        _normalizeService = normalizeService;
        _importConfig = importConfig;
        _dateFormats = importConfig.DateFormats.ToArray();
        _columnGuesser = columnGuesser;
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

        if (
            !TryBuildSchema(
                safeContext.ValidRows,
                safeContext.ColumnCount,
                out var schema,
                out _,
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
                Rows = BuildFallbackPreviewRows(safeContext, previewRowCount),
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
        var previewRows = BuildPreviewRowsWithStatus(safeContext, schema);

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

        var dataRowsForParsing = safeContext.ValidRows
            .Where(row => row.Classification == RowClassification.DataCandidate)
            .ToList();
        ParseRows(dataRowsForParsing, manualSchema, result);

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
        var dataRowsForParsing = dataRows
            .Where(row => row.Classification == RowClassification.DataCandidate)
            .ToList();
        ParseRows(dataRowsForParsing, schema, result);

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
        var lines = normalized.Split('\n');
        var rows = new List<InputRow>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var value = lines[index].Trim();
            rows.Add(new InputRow(value, index + 1));
        }

        return rows;
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

        if (!inputRows.Any(row => !string.IsNullOrWhiteSpace(row.Text)))
        {
            errors.Add("Ingen användbar text hittades.");
            return false;
        }

        if (!TryDetectLayout(inputRows, out var layout, out var detectionError))
        {
            errors.Add(detectionError ?? "Kunde inte avgöra separatorn för importen.");
            return false;
        }

        var parsedRows = layout.ParsedRows;
        FilterRows(parsedRows, layout.ColumnCount, out var validRows, out var ignoredRows);

        foreach (var row in validRows)
        {
            CleanRowCells(row);
        }

        context = new TransactionImportProcessingContext(
            layout.Separator,
            layout.ColumnCount,
            layout.Confidence,
            parsedRows,
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
        if (!sampleRows.Any(row => !string.IsNullOrWhiteSpace(row.Text)))
        {
            error = "Ingen användbar text hittades.";
            return false;
        }

        LayoutCandidate? bestCandidate = null;

        foreach (var candidate in _importConfig.CandidateSeparators)
        {
            var classifiedSamples = sampleRows
                .Select(row => ClassifyRow(row, candidate))
                .ToList();

            var dataCandidates = classifiedSamples
                .Where(row => row.Classification == RowClassification.DataCandidate)
                .ToList();

            if (!dataCandidates.Any())
            {
                dataCandidates = rows
                    .Select(row => ClassifyRow(row, candidate))
                    .Where(row => row.Classification == RowClassification.DataCandidate)
                    .ToList();
            }

            if (!dataCandidates.Any())
            {
                continue;
            }

            var columnGroups = dataCandidates
                .GroupBy(row => row.Columns.Length)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Key)
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

        var classifiedRows = rows
            .Select(row => ClassifyRow(row, bestCandidate.Separator))
            .ToList();

        var sampleClassified = classifiedRows.Take(sampleRows.Count).ToList();
        var sampleDataCandidates = sampleClassified
            .Where(row => row.Classification == RowClassification.DataCandidate)
            .ToList();

        var sampleCount = sampleDataCandidates.Count;
        var matchingCount = sampleDataCandidates.Count(row => row.Columns.Length == bestCandidate.ColumnCount);

        var confidence = sampleCount > 0
            ? Math.Min(1m, matchingCount / (decimal)sampleCount)
            : 0m;

        result = new LayoutDetectionResult(
            bestCandidate.Separator,
            bestCandidate.ColumnCount,
            confidence,
            classifiedRows
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
            if (row.Classification == RowClassification.NonData)
            {
                continue;
            }

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

    private RowData ClassifyRow(InputRow row, char separator)
    {
        var columns = SplitRow(row.Text, separator);

        if (IsHeaderRowCandidate(columns))
        {
            return new RowData(
                row.LineNumber,
                row.Text,
                columns,
                RowClassification.Header,
                "Rubrikrad"
            );
        }

        if (IsNonDataRow(row.Text) || columns.Length < MinLayoutColumnCount)
        {
            return new RowData(
                row.LineNumber,
                row.Text,
                columns,
                RowClassification.NonData,
                "Det här är inte transaktionsdata."
            );
        }

        return new RowData(
            row.LineNumber,
            row.Text,
            columns,
            RowClassification.DataCandidate,
            null
        );
    }

    private bool IsHeaderRowCandidate(string[] columns)
    {
        if (columns.Length == 0)
        {
            return false;
        }

        var matchCount = 0;
        foreach (var column in columns)
        {
            if (!IsHeaderAlias(column))
            {
                continue;
            }

            matchCount++;
            if (matchCount >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNonDataRow(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        var compressed = trimmed.Replace(" ", string.Empty);
        const string separatorChars = "-–_=*.";
        if (compressed.Length > 0 && compressed.All(ch => separatorChars.Contains(ch)))
        {
            return true;
        }

        return false;
    }

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

        var snapshotRows = sampleRows
            .Select(row => new ImportSampleRow(row.LineNumber, row.Columns))
            .ToList();

        var columnMap = _columnGuesser.Guess(snapshotRows, expectedColumnCount);
        var mapErrors = ImportColumnMapValidator.Validate(columnMap);
        if (mapErrors.Count > 0)
        {
            error = string.Join(" ", mapErrors);
            return false;
        }

        var transactionDateIndex = columnMap.TransactionDateIndex ?? columnMap.BookingDateIndex;

        var bookingIndex = columnMap.BookingDateIndex ?? transactionDateIndex;
        schema = new TransactionImportSchema(
            transactionDateIndex.Value,
            columnMap.DescriptionIndex.Value,
            columnMap.AmountIndex.Value,
            bookingIndex,
            columnMap.TransactionTypeIndex,
            columnMap.BalanceIndex,
            expectedColumnCount
        );

        confidence = Math.Min(1m, columnMap.TotalScore / 5m);
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
            ApplySwishTransforms(row, schema);

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

    private static bool ShouldCopySwishTypeToDescription(string? typeRaw)
    {
        if (string.IsNullOrWhiteSpace(typeRaw))
        {
            return false;
        }

        var normalizedType = ImportNormalization.NormalizeText(typeRaw);
        return normalizedType.Contains("swish", StringComparison.Ordinal);
    }

    private void ApplySwishTransforms(RowData row, TransactionImportSchema schema)
    {
        if (
            !_importConfig.Transforms.SwishCopyTypeToDescriptionWhenEmpty
            || !schema.TypeIdx.HasValue
        )
        {
            return;
        }

        var descriptionIdx = schema.DescriptionIdx;
        var typeIdx = schema.TypeIdx.Value;
        if (descriptionIdx == typeIdx)
        {
            return;
        }

        var descriptionValue = row.Columns[descriptionIdx];
        var typeValue = row.Columns[typeIdx];

        if (string.IsNullOrWhiteSpace(descriptionValue) && ShouldCopySwishTypeToDescription(typeValue))
        {
            row.Columns[descriptionIdx] = typeValue ?? string.Empty;
        }
    }

    private static List<TransactionImportPreviewRow> BuildFallbackPreviewRows(
        TransactionImportProcessingContext context,
        int previewRowCount)
    {
        var rows = new List<TransactionImportPreviewRow>(previewRowCount);
        foreach (var row in context.ParsedRows.OrderBy(r => r.LineNumber))
        {
            var status = row.Classification switch
            {
                RowClassification.Header => TransactionImportPreviewRowStatus.Header,
                RowClassification.NonData => TransactionImportPreviewRowStatus.NonData,
                RowClassification.DataCandidate when row.Columns.Length != context.ColumnCount =>
                    TransactionImportPreviewRowStatus.InvalidColumnCount,
                _ => TransactionImportPreviewRowStatus.Accepted,
            };

            string? reason = status switch
            {
                TransactionImportPreviewRowStatus.Header => row.ClassificationReason,
                TransactionImportPreviewRowStatus.NonData => row.ClassificationReason,
                TransactionImportPreviewRowStatus.InvalidColumnCount =>
                    BuildColumnCountMismatchReason(row, context.ColumnCount),
                _ => null,
            };

            rows.Add(CreatePreviewRow(row, status, reason));

            if (rows.Count >= previewRowCount)
            {
                break;
            }
        }

        return rows;
    }

    private List<TransactionImportPreviewRow> BuildPreviewRowsWithStatus(
        TransactionImportProcessingContext context,
        TransactionImportSchema schema)
    {
        var rows = new List<TransactionImportPreviewRow>(context.ParsedRows.Count);
        foreach (var row in context.ParsedRows.OrderBy(r => r.LineNumber))
        {
            if (row.Classification == RowClassification.Header)
            {
                rows.Add(
                    CreatePreviewRow(
                        row,
                        TransactionImportPreviewRowStatus.Header,
                        row.ClassificationReason
                    )
                );
                continue;
            }

            if (row.Classification == RowClassification.NonData)
            {
                rows.Add(
                    CreatePreviewRow(
                        row,
                        TransactionImportPreviewRowStatus.NonData,
                        row.ClassificationReason
                    )
                );
                continue;
            }

            if (row.Columns.Length != context.ColumnCount)
            {
                rows.Add(
                    CreatePreviewRow(
                        row,
                        TransactionImportPreviewRowStatus.InvalidColumnCount,
                        BuildColumnCountMismatchReason(row, context.ColumnCount)
                    )
                );
                continue;
            }

            ApplySwishTransforms(row, schema);

            if (TryValidateBasic(row, schema, out var validationReason))
            {
                rows.Add(CreatePreviewRow(row));
            }
            else
            {
                rows.Add(
                    CreatePreviewRow(
                        row,
                        TransactionImportPreviewRowStatus.ParseError,
                        validationReason
                    )
                );
            }
        }

        return rows;
    }

    private static string BuildColumnCountMismatchReason(RowData row, int expectedColumnCount)
    {
        return $"Rad {row.LineNumber}: Raden innehåller {row.Columns.Length} kolumner men {expectedColumnCount} förväntades.";
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

    private static TransactionImportPreviewRow CreatePreviewRow(
        RowData row,
        TransactionImportPreviewRowStatus status = TransactionImportPreviewRowStatus.Accepted,
        string? ignoredReason = null)
    {
        return new TransactionImportPreviewRow
        {
            LineNumber = row.LineNumber,
            RawRow = row.RawRow,
            Columns = row.Columns.ToArray(),
            Status = status,
            IgnoredReason = ignoredReason,
        };
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
            if (IsHeaderRowCandidate(rows[i].Columns))
            {
                return i;
            }
        }

        return null;
    }

    private bool IsHeaderAlias(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = ImportNormalization.NormalizeHeader(input);
        return _importConfig.HeaderAliasesNormalized.ContainsKey(normalized);
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
        return ImportValueParser.TryParseDate(input, _dateFormats, out date);
    }

    private static bool TryParseAmount(string input, out decimal? amount)
    {
        return ImportValueParser.TryParseAmount(input, out amount);
    }

    private sealed record InputRow(string Text, int LineNumber);

    private sealed record RowData(
        int LineNumber,
        string RawRow,
        string[] Columns,
        RowClassification Classification,
        string? ClassificationReason
    );

    private enum RowClassification
    {
        Header,
        NonData,
        DataCandidate,
    }

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
        List<RowData> ParsedRows,
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

}
