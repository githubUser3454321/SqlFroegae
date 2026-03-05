using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SqlFroega.SsmsExtension;

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("tenantContext")] string? TenantContext);

internal sealed record LoginResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken);

internal sealed record ScriptListItem(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("numberId")] int NumberId,
    [property: JsonPropertyName("scopeLabel")] string ScopeLabel,
    [property: JsonPropertyName("mainModule")] string? MainModule,
    [property: JsonPropertyName("description")] string? Description);

internal sealed record ScriptDetail(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("numberId")] int NumberId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("scopeLabel")] string ScopeLabel,
    [property: JsonPropertyName("mainModule")] string? MainModule,
    [property: JsonPropertyName("relatedModules")] IReadOnlyList<string>? RelatedModules,
    [property: JsonPropertyName("folderId")] Guid? FolderId,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags);

internal sealed record ScriptFolderTreeNode(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parentId")] Guid? ParentId,
    [property: JsonPropertyName("sortOrder")] int SortOrder,
    [property: JsonPropertyName("children")] IReadOnlyList<ScriptFolderTreeNode>? Children);
