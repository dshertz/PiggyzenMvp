using System.Collections.Generic;
using PiggyzenMvp.API.Services.Imports;

namespace PiggyzenMvp.API.Services.Imports.ColumnGuessing;

public sealed record ColumnProfilingResult(
    IReadOnlyList<ColumnProfile> Profiles,
    IReadOnlyList<ImportSampleRow> SampleRows
);
