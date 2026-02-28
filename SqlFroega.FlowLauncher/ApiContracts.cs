using System.Text.Json.Serialization;

namespace SqlFroega.FlowLauncher;

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("tenantContext")] string? TenantContext);

internal sealed record RefreshRequest([property: JsonPropertyName("refreshToken")] string RefreshToken);

internal sealed record LoginResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("accessTokenExpiresAtUtc")] DateTime AccessTokenExpiresAtUtc,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("refreshTokenExpiresAtUtc")] DateTime RefreshTokenExpiresAtUtc);

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
    [property: JsonPropertyName("content")] string Content);

internal sealed record CustomerMappingItem(
    [property: JsonPropertyName("customerCode")] string CustomerCode,
    [property: JsonPropertyName("customerName")] string CustomerName);

internal sealed record RenderRequest([property: JsonPropertyName("sql")] string Sql);

internal sealed record RenderResponse(
    [property: JsonPropertyName("customerCode")] string CustomerCode,
    [property: JsonPropertyName("renderedSql")] string RenderedSql);
