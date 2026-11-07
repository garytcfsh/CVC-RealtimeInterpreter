using System.Windows.Controls;
using CVC.RealtimeInterpreter.Desktop.ViewModels;

namespace CVC.RealtimeInterpreter.Desktop.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
