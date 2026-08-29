using Avalonia.Controls;
using Avalonia.Interactivity;
using GranTurismoTelemetry.ViewModels;

namespace GranTurismoTelemetry.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            if (DataContext is MainViewModel vm)
                vm.UpdateWindowSize(e.NewSize.Width, e.NewSize.Height);
        };
        Opened += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.UpdateWindowSize(ClientSize.Width, ClientSize.Height);
        };
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();
}
