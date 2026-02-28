using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Infrastructure.Parsing;
using SqlFroega.Infrastructure.Persistence;
using SqlFroega.Infrastructure.Persistence.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SqlServerOptions>(builder.Configuration.GetSection("SqlServer"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IHostIdentityProvider, HostIdentityProvider>();
builder.Services.AddScoped<IScriptRepository, ScriptRepository>();
builder.Services.AddScoped<ICustomerMappingRepository, CustomerMappingRepository>();
builder.Services.AddScoped<IUserRepository, SqlUserRepository>();
builder.Services.AddScoped<ISqlCustomerRenderService, SqlCustomerRenderService>();

builder.Services.AddProblemDetails();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ScriptsRead, policy => policy.RequireAssertion(c => HasScope(c.User, "scripts.read", "scripts.write")));
    options.AddPolicy(Policies.ScriptsWrite, policy => policy.RequireAssertion(c => HasScope(c.User, "scripts.write")));
    options.AddPolicy(Policies.MappingsRead, policy => policy.RequireAssertion(c => HasScope(c.User, "mappings.read")));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

var api = app.MapGroup("/api/v1");

api.MapPost("/auth/login", async (LoginRequest request, IUserRepository userRepository) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["credentials"] = ["Username und Passwort sind erforderlich."]
        });
    }

    var user = await userRepository.FindActiveByCredentialsAsync(request.Username, request.Password);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var scopes = user.IsAdmin
        ? new[] { "scripts.read", "scripts.write", "mappings.read", "admin.users" }
        : new[] { "scripts.read", "scripts.write", "mappings.read" };

    var expiresAtUtc = DateTime.UtcNow.AddMinutes(jwtOptions.AccessTokenMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id == Guid.Empty ? user.Username : user.Id.ToString()),
        new(JwtRegisteredClaimNames.UniqueName, user.Username),
        new(ClaimTypes.Name, user.Username)
    };

    claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

    var jwt = new JwtSecurityToken(
        issuer: jwtOptions.Issuer,
        audience: jwtOptions.Audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: expiresAtUtc,
        signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

    var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

    return Results.Ok(new LoginResponse(accessToken, "Bearer", (int)TimeSpan.FromMinutes(jwtOptions.AccessTokenMinutes).TotalSeconds, expiresAtUtc));
});

api.MapGet("/scripts", async (
    [AsParameters] ScriptSearchQuery query,
    IScriptRepository scriptRepository,
    CancellationToken ct) =>
{
    var filters = new ScriptSearchFilters(
        query.Scope,
        query.CustomerId,
        query.Module,
        query.MainModule,
        query.RelatedModule,
        ParseCsv(query.Tags),
        query.ReferencedObject,
        query.IncludeDeleted,
        query.SearchHistory);

    var scripts = await scriptRepository.SearchAsync(query.Query, filters, query.Take, query.Skip, ct);
    return Results.Ok(scripts);
}).RequireAuthorization(Policies.ScriptsRead);

api.MapGet("/scripts/{id:guid}", async (Guid id, IScriptRepository scriptRepository, CancellationToken ct) =>
{
    var script = await scriptRepository.GetByIdAsync(id, ct);
    return script is null ? Results.NotFound() : Results.Ok(script);
}).RequireAuthorization(Policies.ScriptsRead);

api.MapPost("/scripts", async (UpsertScriptRequest request, IScriptRepository scriptRepository, ClaimsPrincipal user, CancellationToken ct) =>
{
    var updatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy) ? user.Identity?.Name : request.UpdatedBy;
    var scriptId = await scriptRepository.UpsertAsync(
        new ScriptUpsert(
            request.Id,
            request.Name,
            request.Content,
            request.Scope,
            request.CustomerId,
            request.MainModule,
            request.RelatedModules ?? Array.Empty<string>(),
            request.Description,
            request.Tags ?? Array.Empty<string>(),
            updatedBy,
            request.UpdateReason),
        ct);

    return Results.Created($"/api/v1/scripts/{scriptId}", new { id = scriptId });
}).RequireAuthorization(Policies.ScriptsWrite);

api.MapDelete("/scripts/{id:guid}", async (Guid id, IScriptRepository scriptRepository, CancellationToken ct) =>
{
    await scriptRepository.DeleteAsync(id, ct);
    return Results.NoContent();
}).RequireAuthorization(Policies.ScriptsWrite);

api.MapPost("/scripts/{id:guid}/locks/acquire", async (Guid id, ScriptLockRequest request, IScriptRepository scriptRepository, ClaimsPrincipal user, CancellationToken ct) =>
{
    var username = request.Username ?? user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["username"] = ["Username ist erforderlich."] });
    }

    var result = await scriptRepository.TryAcquireEditLockAsync(id, username, ct);
    return Results.Ok(result);
}).RequireAuthorization(Policies.ScriptsWrite);

api.MapPost("/scripts/{id:guid}/locks/release", async (Guid id, ScriptLockRequest request, IScriptRepository scriptRepository, ClaimsPrincipal user, CancellationToken ct) =>
{
    var username = request.Username ?? user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["username"] = ["Username ist erforderlich."] });
    }

    await scriptRepository.ReleaseEditLockAsync(id, username, ct);
    return Results.NoContent();
}).RequireAuthorization(Policies.ScriptsWrite);

api.MapGet("/customers/mappings", async (ICustomerMappingRepository customerMappingRepository, CancellationToken ct) =>
{
    var items = await customerMappingRepository.GetAllAsync(ct);
    return Results.Ok(items);
}).RequireAuthorization(Policies.MappingsRead);

api.MapPost("/render/{customerCode}", async (string customerCode, RenderSqlRequest request, ISqlCustomerRenderService renderService, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Sql))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["sql"] = ["SQL darf nicht leer sein."] });
    }

    var renderedSql = await renderService.RenderForCustomerAsync(request.Sql, customerCode, ct);
    return Results.Ok(new { customerCode, renderedSql });
}).RequireAuthorization(Policies.ScriptsRead);

app.Run();

static bool HasScope(ClaimsPrincipal user, params string[] requiredScopes)
{
    var availableScopes = user.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    return requiredScopes.Any(availableScopes.Contains);
}

static IReadOnlyList<string>? ParseCsv(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return null;
    }

    return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

internal static class Policies
{
    public const string ScriptsRead = "scripts.read";
    public const string ScriptsWrite = "scripts.write";
    public const string MappingsRead = "mappings.read";
}

internal sealed record LoginRequest(string Username, string Password);

internal sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresInSeconds, DateTime ExpiresAtUtc);

internal sealed record ScriptSearchQuery(
    string? Query,
    int? Scope,
    Guid? CustomerId,
    string? Module,
    string? MainModule,
    string? RelatedModule,
    string? Tags,
    string? ReferencedObject,
    bool IncludeDeleted = false,
    bool SearchHistory = false,
    int Take = 200,
    int Skip = 0);

internal sealed record UpsertScriptRequest(
    Guid? Id,
    string Name,
    string Content,
    int Scope,
    Guid? CustomerId,
    string? MainModule,
    IReadOnlyList<string>? RelatedModules,
    string? Description,
    IReadOnlyList<string>? Tags,
    string? UpdatedBy,
    string? UpdateReason);

internal sealed record ScriptLockRequest(string? Username);

internal sealed record RenderSqlRequest(string Sql);

internal sealed class JwtOptions
{
    public string Issuer { get; init; } = "SqlFroega";
    public string Audience { get; init; } = "SqlFroega.Extensions";
    public string SigningKey { get; init; } = "replace-me-with-at-least-32-characters";
    public int AccessTokenMinutes { get; init; } = 15;
}
