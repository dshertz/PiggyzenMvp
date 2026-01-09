using System.Collections.Generic;

namespace PiggyzenMvp.API.DTOs;

public class TransactionImportSchemaDefinition
{
    public int ColumnCount { get; set; }

    public int TransactionDateIndex { get; set; }
    public int DescriptionIndex { get; set; }
    public int AmountIndex { get; set; }

    public int? BookingDateIndex { get; set; }
    public int? TransactionKindIndex { get; set; }
    public int? BalanceIndex { get; set; }

    public bool TryValidate(int columnCount, out List<string> errors)
    {
        errors = new List<string>();

        if (columnCount <= 0)
        {
            errors.Add("Kolumnantalet måste vara större än noll.");
            return false;
        }

        if (ColumnCount != columnCount)
        {
            errors.Add($"Schemakolumnantalet ({ColumnCount}) matchar inte importens kolumnantal ({columnCount}).");
        }

        var usedIndexes = new HashSet<int>();

        ValidateIndex(TransactionDateIndex, "Transaktionsdatum", columnCount, usedIndexes, errors);
        ValidateIndex(DescriptionIndex, "Beskrivning", columnCount, usedIndexes, errors);
        ValidateIndex(AmountIndex, "Belopp", columnCount, usedIndexes, errors);

        ValidateOptionalIndex(BookingDateIndex, "Bokföringsdatum", columnCount, usedIndexes, errors);
        ValidateOptionalIndex(TransactionKindIndex, "Transaktionstyp", columnCount, usedIndexes, errors);
        ValidateOptionalIndex(BalanceIndex, "Saldo", columnCount, usedIndexes, errors);

        return errors.Count == 0;
    }

    private static void ValidateIndex(
        int index,
        string name,
        int columnCount,
        HashSet<int> usedIndexes,
        List<string> errors
    )
    {
        if (index < 0 || index >= columnCount)
        {
            errors.Add($"{name}-kolumnen ({index}) ligger utanför intervallet 0-{columnCount - 1}.");
            return;
        }

        if (!usedIndexes.Add(index))
        {
            errors.Add($"{name}-kolumnen delar kolumnindexet {index} med ett annat fält.");
        }
    }

    private static void ValidateOptionalIndex(
        int? index,
        string name,
        int columnCount,
        HashSet<int> usedIndexes,
        List<string> errors
    )
    {
        if (!index.HasValue)
        {
            return;
        }

        ValidateIndex(index.Value, name, columnCount, usedIndexes, errors);
    }
}
