using System.Runtime.InteropServices;
using Sentry;

namespace GranTurismoTelemetry;

/// <summary>
/// Blocking error UI for unhandled exceptions. Shows a message; does not hide the failure.
/// Also reports the exception to Sentry when the SDK is initialized.
/// </summary>
internal static class CrashDialog
{
    public static void Show(Exception ex)
    {
        try { Console.Error.WriteLine(ex); } catch { /* ignore */ }

        try
        {
            SentrySdk.CaptureException(ex);
            SentrySdk.Flush(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // Never let reporting replace the local crash UI.
        }

        string caption = "SlickDash";
        string text =
            "SlickDash hit an unexpected error.\n\n" +
            ex.GetType().Name + ": " + ex.Message;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                MessageBoxW(IntPtr.Zero, text, caption, 0x00000010); // MB_ICONERROR
                return;
            }
        }
        catch
        {
            // fall through to console-only
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
