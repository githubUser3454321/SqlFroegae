using System.Threading;
using System.Windows;

namespace SqlFroega.FlowLauncher;

internal static class ClipboardHelper
{
    public static void SetText(string text)
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.SetText(text ?? string.Empty);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }
    }
}
