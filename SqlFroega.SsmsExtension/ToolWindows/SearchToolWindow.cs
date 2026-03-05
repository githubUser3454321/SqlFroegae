using System;
using Microsoft.VisualStudio.Shell;

namespace SqlFroega.SsmsExtension.ToolWindows;

public sealed class SearchToolWindow : ToolWindowPane
{
    public SearchToolWindow() : base(null)
    {
        Caption = "SqlFroega Search";
        Content = new SearchToolWindowControl();
    }
}
