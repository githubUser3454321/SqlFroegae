using System.Collections.Generic;

namespace SqlFroega.Application.Models;

public sealed record ScriptMetadataCatalog(
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Tags
);
