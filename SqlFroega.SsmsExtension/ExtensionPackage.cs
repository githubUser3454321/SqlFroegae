using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace SqlFroega.SsmsExtension;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuidString)]
public sealed class ExtensionPackage : AsyncPackage
{
    public const string PackageGuidString = "7f5953d2-8d73-4ad4-8fb0-ca1876f4a905";

    protected override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        // Initial bootstrap: später werden hier Commands, ToolWindows und API-Clients verdrahtet.
        return Task.CompletedTask;
    }
}
