using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;

namespace SqlFroega.FlowLauncher;

internal static class ClipboardHelper
{
    public static void SetText(string text)
    {
        Exception? lastError = null;
        var value = text ?? string.Empty;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var (completed, error) = TrySetTextOnce(value, TimeSpan.FromMilliseconds(350));
            if (completed && error is null)
            {
                return;
            }

            lastError = error ?? new TimeoutException("Clipboard operation timed out.");
            if (error is not ExternalException && error is not null)
            {
                throw error;
            }

            Thread.Sleep(60);
        }

        throw lastError ?? new TimeoutException("Clipboard operation timed out.");
    }

    private static (bool Completed, Exception? Error) TrySetTextOnce(string value, TimeSpan timeout)
    {
        Exception? error = null;
        using var completed = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.SetText(value);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        var finished = completed.Wait(timeout);
        return (finished, error);
    }
}
