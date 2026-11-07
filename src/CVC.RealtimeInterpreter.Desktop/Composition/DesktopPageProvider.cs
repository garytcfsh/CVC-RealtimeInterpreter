using CVC.RealtimeInterpreter.Desktop.ViewModels;
using CVC.RealtimeInterpreter.Desktop.Views.Pages;
using Wpf.Ui.Abstractions;

namespace CVC.RealtimeInterpreter.Desktop.Composition;

/// <summary>Owns page instances so navigation preserves the shared session and preferences.</summary>
internal sealed class DesktopPageProvider(MainViewModel viewModel) : INavigationViewPageProvider
{
    private readonly HomePage home = new(viewModel);
    private readonly SettingsPage settings = new(viewModel);
    private readonly AiSettingsPage aiSettings = new(viewModel);

    public object? GetPage(Type pageType)
    {
        if (pageType == typeof(HomePage))
        {
            return home;
        }

        if (pageType == typeof(SettingsPage))
        {
            return settings;
        }

        if (pageType == typeof(AiSettingsPage))
        {
            return aiSettings;
        }

        return null;
    }
}
