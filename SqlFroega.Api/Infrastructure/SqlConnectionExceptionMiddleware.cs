using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SqlFroega.Api.Infrastructure;

public sealed class SqlConnectionExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SqlConnectionExceptionMiddleware> _logger;

    public SqlConnectionExceptionMiddleware(RequestDelegate next, ILogger<SqlConnectionExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SqlException ex) when (IsConnectivityError(ex))
        {
            _logger.LogWarning(ex, "SQL Server Verbindung fehlgeschlagen für {Path}.", context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Datenbank nicht erreichbar",
                Detail = "Die API konnte keine Verbindung zu SQL Server aufbauen. " +
                         "Prüfe die ConnectionString-Konfiguration (SqlServer:ConnectionString) und stelle sicher, " +
                         "dass die SQL-Instanz läuft und erreichbar ist.",
                Instance = context.Request.Path
            };

            await Results.Problem(problem).ExecuteAsync(context);
        }
    }

    private static bool IsConnectivityError(SqlException ex)
        => ex.Number is 2 or 53 or -1 or 4060;
}
