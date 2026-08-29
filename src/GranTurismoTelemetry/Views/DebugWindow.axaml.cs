using Avalonia.Controls;
using Avalonia.Interactivity;
using GranTurismoTelemetry.ViewModels;

namespace GranTurismoTelemetry.Views;

public partial class DebugWindow : UserControl
{
    public DebugWindow() => InitializeComponent();

    private void OnSimToggle(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Telemetry.ApplySource(vm.Settings);
    }
}
