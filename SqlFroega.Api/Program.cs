using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SqlFroega.Api.Auth;
using SqlFroega.Api.Infrastructure;
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
builder.Services.AddSingleton<IRefreshTokenStore>(sp =>
{
    var sqlOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqlServerOptions>>().Value;
    return string.IsNullOrWhiteSpace(sqlOptions.ConnectionString)
        ? new InMemoryRefreshTokenStore()
        : new SqlRefreshTokenStore(sp.GetRequiredService<ISqlConnectionFactory>());
});
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
    options.AddPolicy(Policies.AdminUsers, policy => policy.RequireAssertion(c => HasScope(c.User, "admin.users")));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.Window = TimeSpan.FromSeconds(10);
        limiter.PermitLimit = 100;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 20;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        var statusCode = exception is InvalidOperationException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        context.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == 400 ? "Ungültige Anfrage" : "Serverfehler",
            Detail = exception?.Message,
            Instance = context.Request.Path
        };

        await Results.Problem(problem).ExecuteAsync(context);
    });
});

app.UseCorrelationAndAudit();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

var api = app.MapGroup("/api/v1").RequireRateLimiting("api");

api.MapPost("/auth/login", async (LoginRequest request, IUserRepository userRepository, IRefreshTokenStore refreshTokenStore) =>
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

    var accessToken = CreateAccessToken(user.Username, user.Id, scopes, jwtOptions, signingKey);
    var refresh = await refreshTokenStore.IssueAsync(user.Username, scopes, TimeSpan.FromHours(jwtOptions.RefreshTokenHours));

    return Results.Ok(new LoginResponse(
        accessToken.Token,
        "Bearer",
        accessToken.ExpiresAtUtc,
        refresh.Token,
        refresh.ExpiresAtUtc));
});

api.MapPost("/auth/refresh", async (RefreshTokenRequest request, IRefreshTokenStore refreshTokenStore) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["refreshToken"] = ["Refresh token ist erforderlich."]
        });
    }

    var rotated = await refreshTokenStore.RotateAsync(request.RefreshToken, TimeSpan.FromHours(jwtOptions.RefreshTokenHours));
    if (rotated is null)
    {
        return Results.Unauthorized();
    }

    var accessToken = CreateAccessToken(rotated.Username, Guid.Empty, rotated.Scopes, jwtOptions, signingKey);

    return Results.Ok(new LoginResponse(
        accessToken.Token,
        "Bearer",
        accessToken.ExpiresAtUtc,
        rotated.RefreshToken,
        rotated.RefreshTokenExpiresAtUtc));
});

api.MapPost("/auth/logout", async (RefreshTokenRequest request, IRefreshTokenStore refreshTokenStore) =>
{
    if (!string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        await refreshTokenStore.RevokeAsync(request.RefreshToken);
    }

    return Results.NoContent();
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

api.MapPost("/scripts", async (UpsertScriptRequest request, IScriptRepository scriptRepository, ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct) =>
{
    if (!TryGetTenantContext(httpContext, out var tenantValidationError, out _))
    {
        return tenantValidationError;
    }

    var validationErrors = ValidateUpsertRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

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

api.MapDelete("/scripts/{id:guid}", async (Guid id, IScriptRepository scriptRepository, HttpContext httpContext, CancellationToken ct) =>
{
    if (!TryGetTenantContext(httpContext, out var tenantValidationError, out _))
    {
        return tenantValidationError;
    }

    await scriptRepository.DeleteAsync(id, ct);
    return Results.NoContent();
}).RequireAuthorization(Policies.ScriptsWrite);

api.MapPost("/scripts/{id:guid}/locks/acquire", async (Guid id, ScriptLockRequest request, IScriptRepository scriptRepository, ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct) =>
{
    if (!TryGetTenantContext(httpContext, out var tenantValidationError, out _))
    {
        return tenantValidationError;
    }

    var username = request.Username ?? user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["username"] = ["Username ist erforderlich."] });
    }

    var result = await scriptRepository.TryAcquireEditLockAsync(id, username, ct);
    if (!result.Acquired)
    {
        return Results.Conflict(new
        {
            message = "Script ist bereits gelockt.",
            lockedBy = result.LockedBy
        });
    }

    return Results.Ok(result);
}).RequireAuthorization(Policies.ScriptsWrite);

api.MapPost("/scripts/{id:guid}/locks/release", async (Guid id, ScriptLockRequest request, IScriptRepository scriptRepository, ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct) =>
{
    if (!TryGetTenantContext(httpContext, out var tenantValidationError, out _))
    {
        return tenantValidationError;
    }

    var username = request.Username ?? user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["username"] = ["Username ist erforderlich."] });
    }

    await scriptRepository.ReleaseEditLockAsync(id, username, ct);
    return Results.NoContent();
}).RequireAuthorization(Policies.ScriptsWrite);


api.MapGet("/scripts/{id:guid}/locks/awareness", async (Guid id, string? username, IScriptRepository scriptRepository, ClaimsPrincipal user, CancellationToken ct) =>
{
    var actor = string.IsNullOrWhiteSpace(username) ? user.Identity?.Name : username;
    var awareness = await scriptRepository.GetEditAwarenessAsync(id, actor, ct);
    return awareness is null ? Results.NotFound() : Results.Ok(awareness);
}).RequireAuthorization(Policies.ScriptsRead);

api.MapGet("/customers/mappings", async (ICustomerMappingRepository customerMappingRepository, CancellationToken ct) =>
{
    var items = await customerMappingRepository.GetAllAsync(ct);
    return Results.Ok(items);
}).RequireAuthorization(Policies.MappingsRead);

api.MapGet("/admin/users", async (IUserRepository userRepository) =>
{
    var users = await userRepository.GetAllAsync();
    var result = users.Select(u => new { u.Id, u.Username, u.IsAdmin, u.IsActive });
    return Results.Ok(result);
}).RequireAuthorization(Policies.AdminUsers);

api.MapPost("/render/{customerCode}", async (string customerCode, RenderSqlRequest request, ISqlCustomerRenderService renderService, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Sql))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["sql"] = ["SQL darf nicht leer sein."] });
    }

    if (request.Sql.Length > 200_000)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["sql"] = ["SQL ist zu groß (max. 200000 Zeichen)."] });
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

static bool TryGetTenantContext(HttpContext context, out IResult? errorResult, out string tenantContext)
{
    tenantContext = context.Request.Headers["X-Tenant-Context"].ToString();
    if (string.IsNullOrWhiteSpace(tenantContext))
    {
        errorResult = Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["X-Tenant-Context"] = ["Header X-Tenant-Context ist für schreibende Operationen erforderlich."]
        });
        return false;
    }

    errorResult = null;
    return true;
}

static Dictionary<string, string[]> ValidateUpsertRequest(UpsertScriptRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        errors["name"] = ["Name ist erforderlich."];
    }

    if (string.IsNullOrWhiteSpace(request.Content))
    {
        errors["content"] = ["SQL-Content ist erforderlich."];
    }
    else if (request.Content.Length > 200_000)
    {
        errors["content"] = ["SQL-Content ist zu groß (max. 200000 Zeichen)."];
    }

    if (request.Scope is < 0 or > 2)
    {
        errors["scope"] = ["Scope muss 0 (Global), 1 (Customer) oder 2 (Module) sein."];
    }

    return errors;
}

static AccessTokenResult CreateAccessToken(
    string username,
    Guid userId,
    IReadOnlyList<string> scopes,
    JwtOptions options,
    SymmetricSecurityKey key)
{
    var expiresAtUtc = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, userId == Guid.Empty ? username : userId.ToString()),
        new(JwtRegisteredClaimNames.UniqueName, username),
        new(ClaimTypes.Name, username)
    };

    claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

    var jwt = new JwtSecurityToken(
        issuer: options.Issuer,
        audience: options.Audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: expiresAtUtc,
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

    return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAtUtc);
}

internal static class Policies
{
    public const string ScriptsRead = "scripts.read";
    public const string ScriptsWrite = "scripts.write";
    public const string MappingsRead = "mappings.read";
    public const string AdminUsers = "admin.users";
}

internal sealed record LoginRequest(string Username, string Password);

internal sealed record RefreshTokenRequest(string? RefreshToken);

internal sealed record LoginResponse(string AccessToken, string TokenType, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

internal sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

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
    public int RefreshTokenHours { get; init; } = 12;
}

public partial class Program;
