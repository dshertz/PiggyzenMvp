using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public sealed class ImportColumnGuesser
{
    private readonly ColumnProfiler _profiler;
    private readonly ColumnMappingSolver _solver;
    private readonly ILogger<ImportColumnGuesser> _logger;

    public ImportColumnGuesser(
        ColumnProfiler profiler,
        ColumnMappingSolver solver,
        ILogger<ImportColumnGuesser> logger)
    {
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ImportColumnMap Guess(IReadOnlyList<ImportSampleRow> sampleRows, int columnCount)
    {
        var profiling = _profiler.Profile(sampleRows, columnCount);
        var map = _solver.Solve(profiling);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Guessed columns (score {Score:F2}): booking={Booking}, transaction={Transaction}, type={Type}, description={Description}, amount={Amount}, balance={Balance}.",
                map.TotalScore,
                map.BookingDateIndex?.ToString() ?? "<none>",
                map.TransactionDateIndex?.ToString() ?? "<none>",
                map.TransactionTypeIndex?.ToString() ?? "<none>",
                map.DescriptionIndex?.ToString() ?? "<none>",
                map.AmountIndex?.ToString() ?? "<none>",
                map.BalanceIndex?.ToString() ?? "<none>"
            );
        }

        return map;
    }
}
