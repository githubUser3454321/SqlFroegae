namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class SqlServerOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ScriptsTable { get; set; } = "dbo.SqlScripts";
    public string CustomersTable { get; set; } = "dbo.Customers";

    public bool UseFullTextSearch { get; set; } = false;
    public bool JoinCustomers { get; set; } = true;
}