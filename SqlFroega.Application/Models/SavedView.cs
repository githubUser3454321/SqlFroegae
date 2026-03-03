namespace SqlFroega.Application.Models;

public sealed record SavedView(
    Guid Id,
    string Name,
    string OwnerUsername,
    string Visibility,
    string DefinitionJson,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record SavedViewUpsert(
    Guid? Id,
    string Name,
    string Visibility,
    string DefinitionJson,
    string OwnerUsername);
