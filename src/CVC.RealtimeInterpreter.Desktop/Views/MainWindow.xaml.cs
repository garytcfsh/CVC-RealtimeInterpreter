using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Desktop.ViewModels;
using CVC.RealtimeInterpreter.Desktop.Views.Pages;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CVC.RealtimeInterpreter.Desktop.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel viewModel;
    private bool closingAfterFlush;
    private bool flushingBeforeClose;
    private bool watchingSystemTheme;

    public MainWindow(MainViewModel viewModel, INavigationViewPageProvider pageProvider)
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        // A collapsed child may not enter the logical tree until its group is expanded.
        AiNavigationItem.DataContext = viewModel;
        RootNavigation.SetPageProviderService(pageProvider);
        RootNavigation.ApplyTemplate();
        NavigateTo(typeof(HomePage));
        Rect workArea = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, workArea.Width);
        MinHeight = Math.Min(MinHeight, workArea.Height);
        Width = Math.Min(Width, workArea.Width);
        Height = Math.Min(Height, workArea.Height);
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
        Application.Current.Resources["WindowSurfaceBrush"] = new SolidColorBrush(dark
            ? Color.FromRgb(32, 32, 32)
            : Color.FromRgb(250, 250, 250));
        Application.Current.Resources["WindowTextBrush"] = new SolidColorBrush(dark
            ? Color.FromRgb(245, 245, 245)
            : Color.FromRgb(26, 26, 26));
        Application.Current.Resources["WarningForeground"] = new SolidColorBrush(dark
            ? Color.FromRgb(255, 191, 105)
            : Color.FromRgb(143, 77, 0));
        Application.Current.Resources["CurrentSentenceBackground"] = new SolidColorBrush(dark
            ? Color.FromRgb(27, 54, 82)
            : Color.FromRgb(226, 239, 254));
        Application.Current.Resources["PreviewBackground"] = new SolidColorBrush(dark
            ? Color.FromRgb(45, 48, 54)
            : Color.FromRgb(237, 240, 245));
    }

    public bool NavigateTo(Type pageType)
    {
        if (RootNavigation.SelectedItem?.TargetPageType == pageType)
        {
            return true;
        }

        return RootNavigation.Navigate(pageType);
    }

    private void Navigation_SelectionChanged(object sender, RoutedEventArgs e)
    {
        Type? selectedPage = RootNavigation.SelectedItem?.TargetPageType;
        viewModel.IsSettingsPage = selectedPage == typeof(SettingsPage) ||
            selectedPage == typeof(AiSettingsPage);
        viewModel.IsAiSettingsPage = selectedPage == typeof(AiSettingsPage);

        // A header click must be allowed to collapse its own group while opening General.
        // Reveal the group only when navigating to its AI child.
        if (viewModel.IsAiSettingsPage)
        {
            SettingsNavigationItem.SetCurrentValue(NavigationViewItem.IsExpandedProperty, true);
        }
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
        await viewModel.ShutdownAsync();
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
