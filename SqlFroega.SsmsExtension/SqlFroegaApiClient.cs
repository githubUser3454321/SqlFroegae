using System;
using System.Collections.Generic;
using System.Net;
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
    private readonly object _circuitGate = new();
    private string? _accessToken;
    private int _consecutiveFailures;
    private DateTimeOffset _circuitOpenUntilUtc;

    public SqlFroegaApiClient(HttpClient httpClient, SsmsExtensionSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ScriptListItem>> SearchScriptsAsync(string query, CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        var uri = $"/api/v1/scripts?query={Uri.EscapeDataString(query)}&take={_settings.SearchTake}";

        return await SendAndDeserializeListAsync<ScriptListItem>(() => CreateAuthenticatedRequest(HttpMethod.Get, uri), ct);
    }

    public async Task<IReadOnlyList<ScriptListItem>> GetScriptsByFolderAsync(Guid folderId, CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        var uri = $"/api/v1/scripts?folderId={folderId:D}&folderMustMatchExactly=true&take=500";

        return await SendAndDeserializeListAsync<ScriptListItem>(() => CreateAuthenticatedRequest(HttpMethod.Get, uri), ct);
    }

    public async Task<IReadOnlyList<ScriptFolderTreeNode>> GetFolderTreeAsync(CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        return await SendAndDeserializeListAsync<ScriptFolderTreeNode>(() => CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/folders/tree"), ct);
    }

    public async Task<ScriptDetail> GetScriptDetailAsync(Guid scriptId, CancellationToken ct)
    {
        var response = await GetScriptDetailWithMetadataAsync(scriptId, ct);
        return response.Detail;
    }

    public async Task<ScriptDetailResponse> GetScriptDetailWithMetadataAsync(Guid scriptId, CancellationToken ct)
    {
        await EnsureLoginAsync(ct);

        using var response = await SendWithPolicyAsync(() => CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/scripts/{scriptId}"), ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var detail = await JsonSerializer.DeserializeAsync<ScriptDetail>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Ungültige Script-Detail-Antwort von der API.");

        return new ScriptDetailResponse(detail, response.Headers.ETag?.Tag);
    }

    private async Task<IReadOnlyList<T>> SendAndDeserializeListAsync<T>(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        using var response = await SendWithPolicyAsync(requestFactory, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<IReadOnlyList<T>>(stream, JsonOptions, ct);
        return result ?? Array.Empty<T>();
    }

    private async Task<HttpResponseMessage> SendWithPolicyAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        ThrowIfCircuitOpen();

        HttpResponseMessage? lastResponse = null;
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _settings.HttpRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var request = requestFactory();
                var response = await _httpClient.SendAsync(request, ct);

                if (!ShouldRetry(response.StatusCode) || attempt >= _settings.HttpRetryCount)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        ResetCircuit();
                    }
                    else
                    {
                        RegisterFailure();
                    }

                    return response;
                }

                lastResponse?.Dispose();
                lastResponse = response;
                RegisterFailure();
                await DelayForRetryAsync(attempt, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                RegisterFailure();

                if (attempt >= _settings.HttpRetryCount)
                {
                    throw;
                }

                await DelayForRetryAsync(attempt, ct);
            }
        }

        lastResponse?.Dispose();
        if (lastException is not null)
        {
            throw new InvalidOperationException("API-Aufruf ist nach mehreren Wiederholungen fehlgeschlagen.", lastException);
        }

        throw new InvalidOperationException("API-Aufruf ist nach mehreren Wiederholungen fehlgeschlagen.");
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || numeric >= 500;
    }

    private async Task DelayForRetryAsync(int attempt, CancellationToken ct)
    {
        var factor = attempt + 1;
        var delay = TimeSpan.FromMilliseconds(_settings.HttpRetryDelayMs * factor);
        await Task.Delay(delay, ct);
    }

    private void ThrowIfCircuitOpen()
    {
        lock (_circuitGate)
        {
            if (_circuitOpenUntilUtc > DateTimeOffset.UtcNow)
            {
                var retryIn = (_circuitOpenUntilUtc - DateTimeOffset.UtcNow).TotalSeconds;
                throw new InvalidOperationException($"API-Circuit-Breaker aktiv. Nächster Versuch in ca. {Math.Ceiling(retryIn)}s.");
            }

            if (_circuitOpenUntilUtc != default)
            {
                _circuitOpenUntilUtc = default;
                _consecutiveFailures = 0;
            }
        }
    }

    private void RegisterFailure()
    {
        lock (_circuitGate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _settings.CircuitBreakerFailureThreshold)
            {
                _circuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(_settings.CircuitBreakerBreakSeconds);
                _consecutiveFailures = 0;
            }
        }
    }

    private void ResetCircuit()
    {
        lock (_circuitGate)
        {
            _consecutiveFailures = 0;
            _circuitOpenUntilUtc = default;
        }
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
        ResetCircuit();
    }
}
