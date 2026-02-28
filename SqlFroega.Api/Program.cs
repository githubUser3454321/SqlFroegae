using SqlFroega.Application.Abstractions;
using SqlFroega.Infrastructure.Parsing;
using SqlFroega.Infrastructure.Persistence;
using SqlFroega.Infrastructure.Persistence.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SqlServerOptions>(builder.Configuration.GetSection("SqlServer"));
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IHostIdentityProvider, HostIdentityProvider>();
builder.Services.AddScoped<IScriptRepository, ScriptRepository>();
builder.Services.AddScoped<ICustomerMappingRepository, CustomerMappingRepository>();
builder.Services.AddScoped<IUserRepository, SqlUserRepository>();
builder.Services.AddScoped<ISqlCustomerRenderService, SqlCustomerRenderService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.Run();
