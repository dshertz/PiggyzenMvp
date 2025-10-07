using System.Globalization;
using PiggyzenMvp.API.DTOs;

namespace PiggyzenMvp.API.Services
{
    public class TransactionImportService
    {
        private readonly NormalizeService _normalizeService;

        public List<string> Errors { get; private set; } = new();

        public TransactionImportService(NormalizeService normalizeService)
        {
            _normalizeService = normalizeService;
        }

        public List<TransactionImportDto> ParseRawInput(string rawText)
        {
            Errors.Clear();

            var normalized = NormalizeLineEndings(rawText);
            var rows = SplitIntoRows(normalized);
            return TryStructuredParse(rows);
        }

        private string NormalizeLineEndings(string input)
        {
            return string.IsNullOrWhiteSpace(input)
                ? string.Empty
                : input.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        }

        private List<string> SplitIntoRows(string normalized)
        {
            return normalized
                .Split('\n')
                .Select(row => row.Trim())
                .Where(row => !string.IsNullOrWhiteSpace(row))
                .ToList();
        }

        private List<TransactionImportDto> TryStructuredParse(List<string> rows)
        {
            var results = new List<TransactionImportDto>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int lineNumber = i + 1;

                if (!TryGuessSeparator(row, out var workingSeparator, out var parts))
                {
                    Errors.Add(
                        $"Rad {lineNumber}: Kunde inte tolka – ingen separator gav 5 kolumner. Raden var: \"{row}\""
                    );
                    continue;
                }

                if (!TryParseDate(parts[0], out var bookingDate))
                {
                    Errors.Add($"Rad {lineNumber}: Ogiltigt bokföringsdatum: \"{parts[0]}\"");
                    continue;
                }

                if (!TryParseDate(parts[1], out var transactionDate))
                {
                    Errors.Add($"Rad {lineNumber}: Ogiltigt transaktionsdatum: \"{parts[1]}\"");
                    continue;
                }

                string description = parts[2];

                if (!TryParseAmount(parts[3], out var amount))
                {
                    Errors.Add($"Rad {lineNumber}: Ogiltigt belopp: \"{parts[3]}\"");
                    continue;
                }

                if (!TryParseAmount(parts[4], out var balance))
                {
                    Errors.Add($"Rad {lineNumber}: Ogiltigt saldo: \"{parts[4]}\"");
                    continue;
                }

                results.Add(
                    new TransactionImportDto
                    {
                        BookingDate = bookingDate,
                        TransactionDate = transactionDate!.Value,
                        Description = description,
                        Amount = amount!.Value,
                        Balance = balance,
                        ImportId = GenerateImportId(
                            bookingDate,
                            transactionDate.Value,
                            description,
                            amount.Value,
                            balance
                        ),
                        SourceLineNumber = lineNumber,
                    }
                );
            }

            return results;
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

        private string GenerateImportId(
            DateTime? bookingDate,
            DateTime transactionDate,
            string description,
            decimal amount,
            decimal? balance
        )
        {
            var bookingDatePart = bookingDate?.ToString("yyyyMMdd") ?? "N/A";
            var balancePart = balance?.ToString("F2") ?? "N/A";
            return $"{bookingDatePart}|{transactionDate:yyyyMMdd}|{description}|{amount:F2}|{balancePart}";
        }
    }
}

/* private List<TransactionImportDto> TryStructuredParse(List<string> rows)
{
    var results = new List<TransactionImportDto>();
    char? separator = GuessSeparator(rows);

    if (separator == null)
    {
        Errors.Add(
            "Kunde inte identifiera kolumnseparator (försökte tab, semikolon, komma)."
        );
        return results;
    }

    foreach (var row in rows)
    {
        var parts = row.Split(separator.Value).Select(p => p.Trim()).ToArray();

        if (parts.Length != 5)
        {
            Errors.Add(
                $"Felaktigt antal kolumner ({parts.Length}) i rad: \"{row}\" – förväntar exakt 5 kolumner."
            );
            continue;
        }

        if (!TryParseDate(parts[0], out var bookingDate))
        {
            Errors.Add($"Ogiltigt bokföringsdatum: \"{parts[0]}\" i rad: \"{row}\"");
            continue;
        }

        if (!TryParseDate(parts[1], out var transactionDate))
        {
            Errors.Add($"Ogiltigt transaktionsdatum: \"{parts[1]}\" i rad: \"{row}\"");
            continue;
        }

        string description = parts[2];

        if (!TryParseAmount(parts[3], out var amount))
        {
            Errors.Add($"Ogiltigt belopp: \"{parts[3]}\" i rad: \"{row}\"");
            continue;
        }

        if (!TryParseAmount(parts[4], out var balance))
        {
            Errors.Add($"Ogiltigt saldo: \"{parts[4]}\" i rad: \"{row}\"");
            continue;
        }

        results.Add(
            new TransactionImportDto
            {
                BookingDate = bookingDate,
                TransactionDate = transactionDate!.Value,
                Description = description,
                Amount = amount!.Value,
                Balance = balance,
                ImportId = GenerateImportId(
                    bookingDate,
                    transactionDate.Value,
                    description,
                    amount.Value,
                    balance
                ),
            }
        );
    }

    return results;
} */
