using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GranTurismoTelemetry.Gt7;
using GranTurismoTelemetry.Models;
using GranTurismoTelemetry.ViewModels;
using GranTurismoTelemetry.Views;

namespace GranTurismoTelemetry;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            CrashDialog.Show(e.Exception);
            // Keep the window up long enough to read the dialog; the error was already shown.
            e.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettings.Load();
            var telemetry = new TelemetryService();
            var vm = new MainViewModel(settings, telemetry);
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;
            vm.Start();
            desktop.Exit += (_, _) => vm.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
