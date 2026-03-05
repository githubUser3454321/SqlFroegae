using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using SqlFroega.SsmsExtension.Commands;
using SqlFroega.SsmsExtension.ToolWindows;

namespace SqlFroega.SsmsExtension;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(SearchToolWindow))]
[Guid(PackageGuidString)]
public sealed class ExtensionPackage : AsyncPackage
{
    public const string PackageGuidString = "7f5953d2-8d73-4ad4-8fb0-ca1876f4a905";

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await SearchCommand.InitializeAsync(this);
    }
}
