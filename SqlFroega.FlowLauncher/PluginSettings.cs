namespace SqlFroega.FlowLauncher;

internal sealed class PluginSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DefaultTenantContext { get; set; } = "";
    public string DefaultCustomerCode { get; set; } = "";
    public int SearchCacheSeconds { get; set; } = 60;
    public bool EnableDebugLogging { get; set; } = true;
}
