using System.Globalization;
using PiggyzenMvp.API.DTOs;

namespace PiggyzenMvp.API.Services
{
    public class TransactionImportService
    {
        private readonly NormalizeService _normalizeService;

        public TransactionImportService(NormalizeService normalizeService)
        {
            _normalizeService = normalizeService;
        }

        public TransactionImportParseResult ParseRawInput(string rawText)
        {
            var result = new TransactionImportParseResult();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                result.ParsingErrors.Add("No input provided.");
                return result;
            }

            var normalized = NormalizeLineEndings(rawText);
            var rows = SplitIntoRows(normalized);

            if (rows.Count == 0)
            {
                result.ParsingErrors.Add("No usable rows found.");
                return result;
            }

            TryStructuredParse(rows, result);

            if (!result.Transactions.Any() && !result.ParsingErrors.Any())
            {
                result.ParsingErrors.Add("No valid rows found.");
            }

            return result;
        }

        private string NormalizeLineEndings(string input)
        {
            return input.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        }

        private List<string> SplitIntoRows(string normalized)
        {
            return normalized
                .Split('\n')
                .Select(row => row.Trim())
                .Where(row => !string.IsNullOrWhiteSpace(row))
                .ToList();
        }

        private void TryStructuredParse(List<string> rows, TransactionImportParseResult result)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int lineNumber = i + 1;

                if (!TryGuessSeparator(row, out _, out var parts))
                {
                    result.ParsingErrors.Add(
                        $"Rad {lineNumber}: Kunde inte tolka – ingen separator gav 5 kolumner. Raden var: \"{row}\""
                    );
                    continue;
                }

                if (!TryParseDate(parts[0], out var bookingDate))
                {
                    result.ParsingErrors.Add($"Rad {lineNumber}: Ogiltigt bokföringsdatum: \"{parts[0]}\"");
                    continue;
                }

                if (!TryParseDate(parts[1], out var transactionDate))
                {
                    result.ParsingErrors.Add(
                        $"Rad {lineNumber}: Ogiltigt transaktionsdatum: \"{parts[1]}\""
                    );
                    continue;
                }

                string description = parts[2];

                if (!TryParseAmount(parts[3], out var amount))
                {
                    result.ParsingErrors.Add($"Rad {lineNumber}: Ogiltigt belopp: \"{parts[3]}\"");
                    continue;
                }

                if (!TryParseAmount(parts[4], out var balance))
                {
                    result.ParsingErrors.Add($"Rad {lineNumber}: Ogiltigt saldo: \"{parts[4]}\"");
                    continue;
                }

                var normalizedDescription = _normalizeService.Normalize(description);

                result.Transactions.Add(
                    new TransactionImportDto
                    {
                        BookingDate = bookingDate,
                        TransactionDate = transactionDate!.Value,
                        Description = description,
                        NormalizedDescription = normalizedDescription,
                        Amount = amount!.Value,
                        Balance = balance,
                        ImportId = string.Empty,
                        SourceLineNumber = lineNumber,
                        RawRow = row,
                    }
                );
            }
        }

        private bool TryGuessSeparator(string row, out char? separator, out string[] parts)
        {
            foreach (var sep in new[] { '\t', ';', ',' })
            {
                var split = row.Split(sep).Select(p => p.Trim()).ToArray();
                if (split.Length == 5)
                {
                    separator = sep;
                    parts = split;
                    return true;
                }
            }

            separator = null;
            parts = Array.Empty<string>();
            return false;
        }

        private bool TryParseDate(string input, out DateTime? date)
        {
            var formats = new[]
            {
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "dd-MM-yyyy",
                "dd/MM/yyyy",
                "MM-dd-yyyy",
                "MM/dd/yyyy",
            };

            return DateTime.TryParseExact(
                input,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var d
            )
                ? (date = d) != null
                : (date = null) != null;
        }

        private bool TryParseAmount(string input, out decimal? amount)
        {
            var normalized = input.Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var result
            )
                ? (amount = result) != null
                : (amount = null) != null;
        }

    }
}
