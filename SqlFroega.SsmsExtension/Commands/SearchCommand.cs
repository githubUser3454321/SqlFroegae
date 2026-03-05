using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using SqlFroega.SsmsExtension.ToolWindows;

namespace SqlFroega.SsmsExtension.Commands;

internal sealed class SearchCommand
{
    private readonly AsyncPackage _package;

    private SearchCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package;

        var menuCommandId = new CommandID(PackageGuids.CommandSet, CommandIds.OpenSearchWindowCommandId);
        var menuItem = new MenuCommand(Execute, menuCommandId);
        commandService.AddCommand(menuItem);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        Assumes.Present(commandService);

        _ = new SearchCommand(package, commandService);
    }

    private void Execute(object sender, EventArgs e)
    {
        _ = _package.JoinableTaskFactory.RunAsync(async delegate
        {
            var window = await _package.ShowToolWindowAsync(typeof(SearchToolWindow), 0, true, _package.DisposalToken);
            Assumes.Present(window?.Frame);
        });
    }
}
