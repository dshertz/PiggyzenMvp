namespace PiggyzenMvp.Blazor.Components.Pages.Transactions;

public sealed record FilterChipOption(string Key, string Label, string? ParentKey = null, bool IsGroup = false);

public sealed record FilterChipSection(string? Title, IReadOnlyList<FilterChipOption> Options);
