using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.Models;
using PiggyzenMvp.API.Services;
using PiggyzenMvp.API.Services.Imports;
using PiggyzenMvp.API.Services.Imports.ColumnGuessing;
using ResolvedImportConfig = PiggyzenMvp.API.Services.Imports.ImportConfigService.ResolvedImportConfig;
using Xunit;

namespace PiggyzenMvp.Tests;

public class ImportColumnGuesserTests
{
    [Fact]
    public void GuessFigureOutsDefaultMapping_WhenHeaderlessSample()
    {
        var guesser = CreateGuesser();
        var inputRows = new[]
        {
            new[] { "2025-12-18", "2025-12-18", "Insättning", "PENSION KPA", "73,00" },
            new[] { "2025-12-18", "2025-12-17", "Kortköp", "HOBBEX.SE,STOCKHOLM,SE", "-967,20" },
        };

        var rows = CreateRows(inputRows);
        var map = guesser.Guess(rows, inputRows[0].Length);

        Assert.Equal(0, map.BookingDateIndex);
        Assert.Equal(1, map.TransactionDateIndex);
        Assert.Equal(2, map.TransactionTypeIndex);
        Assert.Equal(3, map.DescriptionIndex);
        Assert.Equal(4, map.AmountIndex);
    }

    [Fact]
    public void GuessResolvesDatesForHeaderlessLfExport()
    {
        var guesser = CreateGuesser();
        var inputRows = new[]
        {
            new[] { "2025-12-18", "2025-12-18", "Insättning", "PENSION KPA", "73,00" },
            new[] { "2025-12-18", "2025-12-17", "Kortköp", "HOBBEX.SE,STOCKHOLM,SE", "-967,20" },
            new[] { "2025-12-17", "2025-12-16", "Kortköp", "STJERNHOLM, ELL,Jonkoping,SE", "-600,00" },
            new[] { "2025-12-17", "2025-12-17", "Överföring", "Daniel Hertz", "1 000,00" },
            new[] { "2025-12-16", "2025-12-16", "Överföring", "Daniel Hertz", "1 000,00" },
            new[] { "2025-12-15", "2025-12-15", "Swish till WALLEY", "MEDS", "-1 149,04" },
        };

        var rows = CreateRows(inputRows);
        var map = guesser.Guess(rows, inputRows[0].Length);

        Assert.Equal(0, map.BookingDateIndex);
        Assert.Equal(1, map.TransactionDateIndex);
    }

    [Fact]
    public void GuessUsesLatestBookingColumn_WhenTwoDateColumnsExist()
    {
        var guesser = CreateGuesser();
        var inputRows = new[]
        {
            new[] { "2025-01-01", "2025-01-02", "Betalning", "Restaurant", "100" },
            new[] { "2025-01-02", "2025-01-03", "Kortköp", "Cinema", "-50" },
        };

        var rows = CreateRows(inputRows);
        var map = guesser.Guess(rows, inputRows[0].Length);

        Assert.Equal(1, map.BookingDateIndex);
        Assert.Equal(0, map.TransactionDateIndex);
    }

    [Fact]
    public void GuessChoosesSignMixColumnForAmount_WhenNegativesExist()
    {
        var guesser = CreateGuesser();
        var inputRows = new[]
        {
            new[] { "2025-02-01", "2025-02-01", "Kortköp", "Garage", "100", "-50" },
            new[] { "2025-02-02", "2025-02-02", "Kortköp", "Gas", "120", "-20" },
        };

        var rows = CreateRows(inputRows);
        var map = guesser.Guess(rows, inputRows[0].Length);

        Assert.Equal(5, map.AmountIndex);
        Assert.Equal(4, map.BalanceIndex);
    }

    [Fact]
    public void GuessFallsBackToMedian_WhenOnlyPositiveAmounts()
    {
        var guesser = CreateGuesser();
        var inputRows = new[]
        {
            new[] { "2025-03-01", "2025-03-01", "Insättning", "Payroll", "110", "1200" },
            new[] { "2025-03-02", "2025-03-02", "Insättning", "Payroll", "120", "1300" },
        };

        var rows = CreateRows(inputRows);
        var map = guesser.Guess(rows, inputRows[0].Length);

        Assert.Equal(4, map.AmountIndex);
        Assert.Equal(5, map.BalanceIndex);
    }

    [Fact]
    public void GuessReportsDuplicateColumns()
    {
        var guesser = CreateGuesser();
        var inputRows = new[]
        {
            new[] { "2025-04-01", "2025-04-01", "Betalning", "Store", "Store", "100" },
            new[] { "2025-04-02", "2025-04-02", "Kortköp", "Store", "Store", "-50" },
        };

        var rows = CreateRows(inputRows);
        var map = guesser.Guess(rows, inputRows[0].Length);

        Assert.Contains(4, map.RedundantColumns);
        Assert.Equal(3, map.DescriptionIndex);
    }

    private static IReadOnlyList<ImportSampleRow> CreateRows(string[][] values)
    {
        var rows = new List<ImportSampleRow>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            rows.Add(new ImportSampleRow(i + 1, values[i]));
        }

        return rows;
    }

    private static ImportColumnGuesser CreateGuesser()
    {
        var options = new DbContextOptionsBuilder<PiggyzenMvpContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new PiggyzenMvpContext(options);
        context.Database.EnsureCreated();

        var normalizeService = new NormalizeService();
        var signatureService = new DescriptionSignatureService(context, normalizeService);
        var cardDetector = new CardPurchaseDetectionService();

        var kindRules = new[]
        {
            new ImportConfigService.KindRule(TransactionKind.CardPurchase, new[] { "kortköp", "kort" }),
            new ImportConfigService.KindRule(TransactionKind.Deposit, new[] { "insättning" }),
            new ImportConfigService.KindRule(TransactionKind.Payment, new[] { "betalning" }),
        };

        var config = new ResolvedImportConfig(
            new[] { '\t', ';', ',' },
            new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy" },
            new Dictionary<string, HeaderField>(StringComparer.Ordinal),
            kindRules,
            new ImportConfigService.ImportTransforms()
        );

        var profiler = new ColumnProfiler(config, signatureService, cardDetector);
        var solver = new ColumnMappingSolver();
        return new ImportColumnGuesser(profiler, solver, NullLogger<ImportColumnGuesser>.Instance);
    }
}
