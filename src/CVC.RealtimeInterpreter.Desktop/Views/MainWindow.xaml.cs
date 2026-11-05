using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Desktop.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CVC.RealtimeInterpreter.Desktop.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel viewModel;
    private bool closingAfterFlush;
    private bool flushingBeforeClose;
    private bool watchingSystemTheme;

    public MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        ApplyTheme();
        Loaded += Window_Loaded;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplicationThemeManager.Changed += ThemeManager_Changed;
        Closing += Window_Closing;
        Closed += Window_Closed;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Theme))
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        if (watchingSystemTheme)
        {
            SystemThemeWatcher.UnWatch(this);
            watchingSystemTheme = false;
        }

        if (viewModel.Theme == ThemePreference.System)
        {
            if (IsLoaded)
            {
                SystemThemeWatcher.Watch(this);
                watchingSystemTheme = true;
            }

            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            ApplicationThemeManager.Apply(viewModel.Theme == ThemePreference.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light);
        }

        UpdateSemanticBrushes();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
    }

    private void ThemeManager_Changed(ApplicationTheme theme, Color accent)
    {
        UpdateSemanticBrushes();
    }

    private void UpdateSemanticBrushes()
    {
        bool dark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
        Resources["WarningForeground"] = new SolidColorBrush(dark
            ? Color.FromRgb(255, 191, 105)
            : Color.FromRgb(143, 77, 0));
        Resources["CurrentSentenceBackground"] = new SolidColorBrush(dark
            ? Color.FromRgb(27, 54, 82)
            : Color.FromRgb(226, 239, 254));
        Resources["PreviewBackground"] = new SolidColorBrush(dark
            ? Color.FromRgb(45, 48, 54)
            : Color.FromRgb(237, 240, 245));
    }

    private void ReturnToLive_Click(object sender, RoutedEventArgs e)
    {
        TranscriptScroller.ScrollToEnd();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closingAfterFlush)
        {
            if (watchingSystemTheme)
            {
                SystemThemeWatcher.UnWatch(this);
                watchingSystemTheme = false;
            }

            return;
        }

        e.Cancel = true;

        if (flushingBeforeClose)
        {
            return;
        }

        flushingBeforeClose = true;
        await viewModel.FlushSettingsAsync();
        closingAfterFlush = true;
        await Dispatcher.InvokeAsync(Close);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ApplicationThemeManager.Changed -= ThemeManager_Changed;
        Loaded -= Window_Loaded;
    }
}
