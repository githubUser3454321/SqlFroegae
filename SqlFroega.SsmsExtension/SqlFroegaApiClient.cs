using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.SsmsExtension;

internal sealed class SqlFroegaApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly SsmsExtensionSettings _settings;
    private string? _accessToken;

    public SqlFroegaApiClient(HttpClient httpClient, SsmsExtensionSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ScriptListItem>> SearchScriptsAsync(string query, CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        var uri = $"/api/v1/scripts?query={Uri.EscapeDataString(query)}&take={_settings.SearchTake}";

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, uri);
        return await SendAndDeserializeListAsync<ScriptListItem>(request, ct);
    }

    public async Task<IReadOnlyList<ScriptListItem>> GetScriptsByFolderAsync(Guid folderId, CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        var uri = $"/api/v1/scripts?folderId={folderId:D}&folderMustMatchExactly=true&take=500";

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, uri);
        return await SendAndDeserializeListAsync<ScriptListItem>(request, ct);
    }

    public async Task<IReadOnlyList<ScriptFolderTreeNode>> GetFolderTreeAsync(CancellationToken ct)
    {
        await EnsureLoginAsync(ct);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/folders/tree");
        return await SendAndDeserializeListAsync<ScriptFolderTreeNode>(request, ct);
    }

    public async Task<ScriptDetail> GetScriptDetailAsync(Guid scriptId, CancellationToken ct)
    {
        await EnsureLoginAsync(ct);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/scripts/{scriptId}");
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<ScriptDetail>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Ungültige Script-Detail-Antwort von der API.");
    }

    private async Task<IReadOnlyList<T>> SendAndDeserializeListAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<IReadOnlyList<T>>(stream, JsonOptions, ct);
        return result ?? Array.Empty<T>();
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", $"ssms-{Guid.NewGuid():N}");

        if (!string.IsNullOrWhiteSpace(_settings.TenantContext))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Context", _settings.TenantContext.Trim());
        }

        return request;
    }

    private async Task EnsureLoginAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException("Bitte SQLFROEGA_USERNAME und SQLFROEGA_PASSWORD für die SSMS-Extension setzen.");
        }

        var requestBody = new LoginRequest(
            _settings.Username,
            _settings.Password,
            string.IsNullOrWhiteSpace(_settings.TenantContext) ? null : _settings.TenantContext);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var loginResponse = await JsonSerializer.DeserializeAsync<LoginResponse>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Ungültige Login-Antwort von der API.");

        _accessToken = loginResponse.AccessToken;
    }
}
