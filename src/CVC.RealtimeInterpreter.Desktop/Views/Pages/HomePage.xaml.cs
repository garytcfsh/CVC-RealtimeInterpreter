using System.Windows;
using System.Windows.Controls;
using CVC.RealtimeInterpreter.Desktop.ViewModels;

namespace CVC.RealtimeInterpreter.Desktop.Views.Pages;

public partial class HomePage : Page
{
    public HomePage(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void ReturnToLive_Click(object sender, RoutedEventArgs e)
    {
        TranscriptScroller.ScrollToEnd();
    }
}
