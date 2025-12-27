using System;
using System.Collections.Generic;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public static class ImportColumnMapValidator
{
    public static IReadOnlyList<string> Validate(ImportColumnMap map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        var errors = new List<string>();

        if (!map.TransactionDateIndex.HasValue && !map.BookingDateIndex.HasValue)
        {
            errors.Add("Kunde inte avgöra vilket fält som är transaktionsdatum eller bokföringsdatum.");
        }

        if (!map.DescriptionIndex.HasValue)
        {
            errors.Add("Kunde inte avgöra vilken kolumn som innehåller transaktionstexten.");
        }

        if (!map.AmountIndex.HasValue)
        {
            errors.Add("Kunde inte avgöra vilken kolumn som innehåller belopp.");
        }

        return errors;
    }
}
