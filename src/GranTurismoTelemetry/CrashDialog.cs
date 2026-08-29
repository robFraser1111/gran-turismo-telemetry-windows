using System.Runtime.InteropServices;

namespace GranTurismoTelemetry;

/// <summary>
/// Blocking error UI for unhandled exceptions. Shows a message; does not hide the failure.
/// </summary>
internal static class CrashDialog
{
    public static void Show(Exception ex)
    {
        try { Console.Error.WriteLine(ex); } catch { /* ignore */ }

        string caption = "Gran Telemetry";
        string text =
            "Gran Telemetry hit an unexpected error.\n\n" +
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
