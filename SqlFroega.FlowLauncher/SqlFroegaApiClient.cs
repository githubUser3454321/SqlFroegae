using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SqlFroega.FlowLauncher;

internal sealed class SqlFroegaApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly PluginSettings _settings;

    private string? _accessToken;
    private string? _refreshToken;

    public SqlFroegaApiClient(HttpClient httpClient, PluginSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ScriptListItem>> SearchScriptsAsync(string query, CancellationToken ct)
    {
        var uri = $"/api/v1/scripts?query={Uri.EscapeDataString(query)}&take=40";
        return await SendAsync<IReadOnlyList<ScriptListItem>>(HttpMethod.Get, uri, null, ct) ?? Array.Empty<ScriptListItem>();
    }

    public async Task<ScriptDetail?> GetScriptDetailAsync(Guid id, CancellationToken ct)
    {
        return await SendAsync<ScriptDetail>(HttpMethod.Get, $"/api/v1/scripts/{id}", null, ct);
    }

    public async Task<RenderResponse?> RenderSqlAsync(string customerCode, string sql, CancellationToken ct)
    {
        return await SendAsync<RenderResponse>(HttpMethod.Post, $"/api/v1/render/{Uri.EscapeDataString(customerCode)}", new RenderRequest(sql), ct);
    }

    public async Task<IReadOnlyList<CustomerMappingItem>> GetCustomerMappingsAsync(CancellationToken ct)
    {
        return await SendAsync<IReadOnlyList<CustomerMappingItem>>(HttpMethod.Get, "/api/v1/customers/mappings", null, ct) ?? Array.Empty<CustomerMappingItem>();
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        await EnsureAccessTokenAsync(ct);

        var response = await SendInternalAsync(method, path, body, retryOnUnauthorized: true, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Authentifizierung fehlgeschlagen. Bitte Plugin-Settings prüfen.");
        }

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is 0)
        {
            return default;
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(contentStream, JsonOptions, ct);
    }

    private async Task<HttpResponseMessage> SendInternalAsync(HttpMethod method, string path, object? body, bool retryOnUnauthorized, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", $"flow-{Guid.NewGuid():N}");

        if (!string.IsNullOrWhiteSpace(_settings.DefaultTenantContext))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Context", _settings.DefaultTenantContext.Trim());
        }

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !retryOnUnauthorized)
        {
            return response;
        }

        response.Dispose();

        await RefreshOrLoginAsync(ct);
        return await SendInternalAsync(method, path, body, retryOnUnauthorized: false, ct);
    }

    private async Task EnsureAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return;
        }

        await LoginAsync(ct);
    }

    private async Task RefreshOrLoginAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_refreshToken))
        {
            var refreshed = await TryRefreshAsync(ct);
            if (refreshed)
            {
                return;
            }
        }

        await LoginAsync(ct);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = new StringContent(JsonSerializer.Serialize(new RefreshRequest(_refreshToken!), JsonOptions), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        var loginResponse = await JsonSerializer.DeserializeAsync<LoginResponse>(contentStream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Ungültige API-Antwort bei Token-Refresh.");

        _accessToken = loginResponse.AccessToken;
        _refreshToken = loginResponse.RefreshToken;
        return true;
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException("Username/Password fehlen in den Plugin-Einstellungen.");
        }

        var login = new LoginRequest(_settings.Username, _settings.Password, string.IsNullOrWhiteSpace(_settings.DefaultTenantContext) ? null : _settings.DefaultTenantContext);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent(JsonSerializer.Serialize(login, JsonOptions), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        var loginResponse = await JsonSerializer.DeserializeAsync<LoginResponse>(contentStream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Ungültige API-Antwort beim Login.");

        _accessToken = loginResponse.AccessToken;
        _refreshToken = loginResponse.RefreshToken;
    }
}
