using Avalonia;
using Sentry;
using System;
using System.Linq;

namespace GranTurismoTelemetry;

sealed class Program
{
    internal const string SentryDsn =
        "https://801b3f05d4f4e7eb0353671292edaf3d@o4511995844231168.ingest.us.sentry.io/4511997531848704";

    [STAThread]
    public static void Main(string[] args)
    {
        using var _ = SentrySdk.Init(options =>
        {
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? SentryDsn;
            options.IsGlobalModeEnabled = true;
            options.SendDefaultPii = true;
            options.TracesSampleRate = 1.0;
            options.Release = "gran-telemetry@" +
                (typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.1");
#if DEBUG
            options.Debug = true;
            options.Environment = "development";
#else
            options.Environment = "production";
#endif
            options.SetBeforeSend((evt, _) =>
            {
                evt.ServerName = null;
                return evt;
            });
        });

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                CrashDialog.Show(ex);
        };

        if (args.Any(a => string.Equals(a, "--sentry-test", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                throw new InvalidOperationException(
                    "Sentry test error " + DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex)
            {
                CrashDialog.Show(ex);
            }

            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            CrashDialog.Show(ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
