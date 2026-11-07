using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using CVC.RealtimeInterpreter.Desktop.ViewModels;

namespace CVC.RealtimeInterpreter.Desktop.Views.Pages;

public partial class AiSettingsPage : Page
{
    private readonly ProviderSettingsViewModel providers;
    private bool clearingKey;
    public AiSettingsPage(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        providers = viewModel.Providers;
        Loaded += (_, _) => providers.PropertyChanged += Providers_PropertyChanged;
        Unloaded += (_, _) =>
        {
            providers.PropertyChanged -= Providers_PropertyChanged;
            ClearKeyEditor();
        };
    }

    private void Providers_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProviderSettingsViewModel.SelectedProfile))
        {
            ClearKeyEditor();
        }
    }

    private void ClearKeyEditor()
    {
        clearingKey = true;
        ApiKeyEditor.Password = string.Empty;
        clearingKey = false;
    }

    private void ApiKeyEditor_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!clearingKey && providers is not null)
        {
            providers.QueueApiKey(ApiKeyEditor.Password);
        }
    }

    private async void RemoveKey_Click(object sender, RoutedEventArgs e)
    {
        await providers.RemoveKeyCommand.ExecuteAsync(null);
        ClearKeyEditor();
    }
}
