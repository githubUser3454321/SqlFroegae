namespace SqlFroega.SsmsExtension.ToolWindows;

internal sealed record SearchResultItem(
    string Name,
    int NumberId,
    string Scope,
    string MainModule,
    string Description);
