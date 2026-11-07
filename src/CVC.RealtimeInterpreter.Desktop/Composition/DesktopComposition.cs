using System.IO;
using System.Text.Json;
using System.Windows;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Core.Audio;
using CVC.RealtimeInterpreter.Core.Platforms.Windows;
using CVC.RealtimeInterpreter.Desktop.ViewModels;
using CVC.RealtimeInterpreter.Desktop.Views;

namespace CVC.RealtimeInterpreter.Desktop.Composition;

internal static class DesktopComposition
{
    public static async Task StartAsync(Application application)
    {
        IStorageLocations locations = new WindowsStorageLocations();
        ISettingsStore store = new JsonSettingsStore(locations.SettingsFile);
        AppSettings settings = new();
        string? loadError = null;

        try
        {
            settings = await store.LoadAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            loadError = "Settings could not be loaded. Defaults are shown; the original file has not been changed. " +
                exception.Message;
        }

        AudioInputController audioInput = new(new WindowsAudioInputBackend(), settings.AudioInputDeviceId);
        MainViewModel viewModel = new(settings, store, locations, loadError, audioInput, new WindowsProviderCredentialStore());
        MainWindow window = new(viewModel, new DesktopPageProvider(viewModel));
        application.MainWindow = window;
        window.Show();
        await viewModel.Providers.InitializeCredentialsAsync();
        await audioInput.RefreshAsync();
    }
}
