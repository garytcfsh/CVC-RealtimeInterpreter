using System.Windows.Controls;
using CVC.RealtimeInterpreter.Desktop.ViewModels;

namespace CVC.RealtimeInterpreter.Desktop.Views.Pages;

public partial class AiSettingsPage : Page
{
    public AiSettingsPage(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
