namespace SqlFroega.Api;

internal static class FolderCollectionRequestValidator
{
    public static Dictionary<string, string[]> ValidateFolderUpsert(ScriptFolderUpsertRequest request, Guid? routeId = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Folder-Name ist erforderlich."];
        }

        if (routeId.HasValue && request.ParentId == routeId.Value)
        {
            errors["parentId"] = ["Ein Ordner kann nicht sein eigener Parent sein."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateCollectionUpsert(ScriptCollectionUpsertRequest request, Guid? routeId = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Collection-Name ist erforderlich."];
        }

        if (routeId.HasValue && request.ParentId == routeId.Value)
        {
            errors["parentId"] = ["Eine Collection kann nicht ihr eigener Parent sein."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateCollectionAssignment(ScriptCollectionAssignmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var distinct = (request.CollectionIds ?? Array.Empty<Guid>()).Distinct().ToArray();

        if (request.PrimaryCollectionId is not null && !distinct.Contains(request.PrimaryCollectionId.Value))
        {
            errors["primaryCollectionId"] = ["PrimaryCollectionId muss in collectionIds enthalten sein."];
        }

        return errors;
    }
}
