using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Reflection;
using CVC.RealtimeInterpreter.Desktop.Composition;
using CVC.RealtimeInterpreter.Desktop.Views.Pages;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Core.Audio;
using CVC.RealtimeInterpreter.Core.Providers;
using CVC.RealtimeInterpreter.Desktop.ViewModels;
using CVC.RealtimeInterpreter.Desktop.Views;
using Wpf.Ui.Controls;

namespace CVC.RealtimeInterpreter.Desktop.Diagnostics;

/// <summary>Renders the real views with sample data, without desktop automation or user-settings access.</summary>
internal static class PreviewRenderer
{
    public static async Task ExportAsync(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);

        foreach (ThemePreference theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            foreach (Size size in new[] { new Size(900, 620), new Size(1280, 850) })
            {
                foreach (int page in new[] { 0, 1, 2 })
                {
                    await RenderAsync(directory, theme, size, page, InterfaceLanguage.English, 24);
                }
            }

            await RenderAsync(directory, theme, new Size(900, 620), 0,
                InterfaceLanguage.TraditionalChinese, 48);
            await RenderAsync(directory, theme, new Size(1280, 850), 1,
                InterfaceLanguage.TraditionalChinese, 24);
            await RenderAsync(directory, theme, new Size(900, 620), 0,
                InterfaceLanguage.English, 24, collapsePane: true);
            foreach (string state in new[] { "capturing", "silence", "no-packets", "disconnected", "denied" })
            {
                await RenderAsync(directory, theme, new Size(1280, 850), 0,
                    InterfaceLanguage.English, 24, audioState: state);
            }
            await RenderAsync(directory, theme, new Size(900, 620), 0,
                InterfaceLanguage.TraditionalChinese, 24, audioState: "capturing");
            await RenderAsync(directory, theme, new Size(900, 620), 2,
                InterfaceLanguage.English, 24, providerTab: true);
            await RenderAsync(directory, theme, new Size(1280, 850), 2,
                InterfaceLanguage.TraditionalChinese, 24, providerTab: true);
            await RenderAsync(directory, theme, new Size(900, 620), 2,
                InterfaceLanguage.TraditionalChinese, 24, providerTab: true, providerModels: true);
        }
    }

    private static async Task RenderAsync(
        string directory,
        ThemePreference theme,
        Size size,
        int page,
        InterfaceLanguage language,
        double fontSize,
        bool collapsePane = false,
        string? audioState = null,
        bool providerTab = false,
        bool providerModels = false)
    {
        AppSettings settings = new()
        {
            Theme = theme,
            Language = language,
            TranscriptFontSize = fontSize,
            AudioInputDeviceId = "preview-mixer",
            Providers = ProviderCatalog.CreateDefaults()
        };
        // Preview assignments are synthetic examples; real application defaults remain unassigned.
        Guid previewProvider = settings.Providers.Profiles[0].Id;
        settings = settings with
        {
            Providers = settings.Providers with
            {
                Stt = new(previewProvider, "gpt-live-transcribe"),
                Translation = new(previewProvider, "gpt-4.1"),
                SpeechGeneration = new(previewProvider, "gpt-realtime-2.1-mini")
            }
        };
        PreviewSettingsStore store = new(settings);
        PreviewAudioBackend backend = new();
        PreviewProviderCredentialStore credentials = new();
        await using AudioInputController audioInput = new(backend, settings.AudioInputDeviceId);
        await audioInput.RefreshAsync();
        MainViewModel viewModel = new(settings, store,
            new PreviewStorageLocations(), null, audioInput, credentials);
        viewModel.LoadPreviewCommand.Execute(null);
        if (audioState is not null)
        {
            if (audioState == "silence")
            {
                backend.SignalPeak = 0;
            }
            else if (audioState == "no-packets")
            {
                backend.SignalPeak = null;
            }

            if (audioState == "denied")
            {
                backend.StartFailure = AudioInputFailure.AccessDenied;
            }

            await viewModel.StartAudioCommand.ExecuteAsync(null);
            if (audioState == "disconnected")
            {
                backend.Devices.RemoveAll(device => device.Id == settings.AudioInputDeviceId);
                await audioInput.RefreshAsync();
            }

            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        DesktopPageProvider pageProvider = new(viewModel);
        MainWindow window = new(viewModel, pageProvider);
        Type requestedPage = page switch
        {
            0 => typeof(HomePage),
            1 => typeof(SettingsPage),
            2 => typeof(AiSettingsPage),
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };

        if (!window.NavigateTo(requestedPage))
        {
            throw new InvalidOperationException("The requested preview page could not be opened.");
        }

        if (!window.NavigateTo(page == 0 ? typeof(SettingsPage) : typeof(HomePage)) ||
            !window.NavigateTo(requestedPage) ||
            viewModel.Segments.Count != (audioState is null ? 3 : 0) || viewModel.IsSettingsPage != (page != 0) ||
            viewModel.IsAiSettingsPage != (page == 2))
        {
            throw new InvalidOperationException("Navigation did not preserve page/session state.");
        }

        NavigationViewItem settingsHeader = (NavigationViewItem)window.FindName("SettingsNavigationItem");

        if (page != 0)
        {
            settingsHeader.SetCurrentValue(NavigationViewItem.IsExpandedProperty, true);
        }

        // A non-visible presentation source runs normal Loaded/Unloaded behavior without
        // showing a window, taking screenshots, or accessing real settings/provider services.
        FrameworkElement content = (FrameworkElement)window.Content;
        using HwndSource presentationSource = new(new HwndSourceParameters("CVC visual preview")
        {
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, deliberately without WS_VISIBLE.
            Width = (int)size.Width,
            Height = (int)size.Height
        });
        presentationSource.RootVisual = content;
        content.Measure(size);
        content.Arrange(new Rect(size));
        content.UpdateLayout();
        await Application.Current.Dispatcher.InvokeAsync(
            () => { }, DispatcherPriority.ApplicationIdle);
        // NavigationView's pane has a short native WPF animation even with page transitions off.
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        content.UpdateLayout();

        NavigationView navigation = (NavigationView)window.FindName("RootNavigation");

        if (theme == ThemePreference.Light && size.Width == 1280 && page == 1 &&
            language == InterfaceLanguage.English)
        {
            await VerifyPreferenceSelectionsAsync(window, pageProvider, viewModel, store);
            await VerifyPaneToggleAsync(navigation);
            await VerifySettingsHeaderAsync(window, navigation, viewModel);
            await VerifyAudioControlsAsync(pageProvider, viewModel, audioInput, backend, store);
            await VerifyProviderSettingsAsync(window, pageProvider, viewModel, credentials, store);
            window.NavigateTo(requestedPage);
            settingsHeader.SetCurrentValue(NavigationViewItem.IsExpandedProperty, true);
            viewModel.Theme = theme;
            viewModel.Language = language;
            viewModel.Voice = settings.Voice;
            await viewModel.FlushSettingsAsync();
            await SettleLayoutAsync(content);
        }

        if (collapsePane)
        {
            TogglePane(navigation);
            await SettleLayoutAsync(content);
        }

        if (providerTab)
        {
            AiSettingsPage aiPage = (AiSettingsPage)pageProvider.GetPage(typeof(AiSettingsPage))!;
            ((TabView)aiPage.FindName("AiTabs")).SelectedIndex = 1;
            await SettleLayoutAsync(content);
            if (providerModels)
            {
                ((ComboBox)aiPage.FindName("ConfiguredModelSelector")).SelectedIndex = 0;
                ScrollViewer editorScroller = (ScrollViewer)aiPage.FindName("ProviderEditorScroller");
                editorScroller.ScrollToEnd();
                await SettleLayoutAsync(content);
                if (editorScroller.ScrollableHeight <= 0 || editorScroller.VerticalOffset <= 0)
                {
                    throw new InvalidOperationException("The provider editor must scroll within its tab so all model fields remain reachable.");
                }
            }
        }

        TitleBar titleBar = (TitleBar)window.FindName("ApplicationTitleBar");
        await Application.Current.Dispatcher.InvokeAsync(
            () => { }, DispatcherPriority.ApplicationIdle);
        content.UpdateLayout();

        if (titleBar.ActualHeight < 30 || !titleBar.ShowClose || !titleBar.ShowMinimize ||
            !titleBar.ShowMaximize || titleBar.ForceShutdown || CountCaptionButtons(titleBar) < 3)
        {
            throw new InvalidOperationException("The rendered title bar is missing its window controls.");
        }

        NavigationViewItem homeItem = (NavigationViewItem)window.FindName("HomeNavigationItem");
        NavigationViewItem settingsItem = (NavigationViewItem)window.FindName("SettingsNavigationItem");
        NavigationViewItem aiItem = (NavigationViewItem)window.FindName("AiNavigationItem");
        await WaitForNavigationLayoutAsync(content, homeItem, collapsePane);

        if ((!collapsePane && homeItem.ActualWidth < 180) ||
            (collapsePane && (navigation.IsPaneOpen || homeItem.ActualWidth > 60)) ||
            homeItem.TranslatePoint(new Point(), content).Y < 40 ||
            !Equals(homeItem.Content, viewModel.HomeLabel) ||
            !Equals(settingsItem.Content, viewModel.SettingsTitle) ||
            settingsItem.TargetPageType != typeof(SettingsPage) ||
            settingsItem.MenuItems.Count != 1 ||
            !ReferenceEquals(settingsItem.MenuItems[0], aiItem) ||
            !Equals(aiItem.Content, viewModel.AiLabel))
        {
            throw new InvalidOperationException(
                $"Invalid navigation: width={homeItem.ActualWidth}, y={homeItem.TranslatePoint(new Point(), content).Y}, " +
                $"home={homeItem.Content}, settings={settingsItem.Content}, target={settingsItem.TargetPageType}, " +
                $"children={settingsItem.MenuItems.Count}, ai={aiItem.Content}, " +
                $"childMatches={ReferenceEquals(settingsItem.MenuItems[0], aiItem)}.");
        }

        RenderTargetBitmap bitmap = new((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string pageName = page switch
        {
            0 => "home",
            1 => "general",
            _ => "ai"
        };
        string fileName = $"{theme.ToString().ToLowerInvariant()}-{pageName}-" +
            $"{(int)size.Width}x{(int)size.Height}-{language}-{fontSize:0}" +
            (collapsePane ? "-collapsed" : string.Empty) + ".png";
        if (audioState is not null)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName) + "-" + audioState + ".png";
        }
        if (providerTab)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName) + "-connections.png";
        }
        if (providerModels)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName) + "-models.png";
        }
        using (FileStream stream = File.Create(Path.Combine(directory, fileName)))
        {
            encoder.Save(stream);
        }

        presentationSource.RootVisual = null;
        window.Close();
        await Application.Current.Dispatcher.InvokeAsync(
            () => { }, DispatcherPriority.ApplicationIdle);
    }

    private static int CountCaptionButtons(DependencyObject element)
    {
        int count = element is TitleBarButton { Visibility: Visibility.Visible } ? 1 : 0;

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            count += CountCaptionButtons(VisualTreeHelper.GetChild(element, index));
        }

        return count;
    }

    private static async Task WaitForNavigationLayoutAsync(FrameworkElement content, NavigationViewItem home, bool collapsed)
    {
        // Wait for actual geometry, not just an assumed animation duration on a busy machine.
        double previous = -1;
        int stable = 0;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(50);
            content.UpdateLayout();
            double width = home.ActualWidth;
            bool reached = collapsed ? width <= 60 : width >= 180;
            stable = reached && Math.Abs(width - previous) < 0.1 ? stable + 1 : 0;
            if (stable >= 2)
            {
                return;
            }

            previous = width;
        }
    }

    private static async Task VerifyPreferenceSelectionsAsync(
        MainWindow window,
        DesktopPageProvider pageProvider,
        MainViewModel viewModel,
        PreviewSettingsStore store)
    {
        SettingsPage general = (SettingsPage)pageProvider.GetPage(typeof(SettingsPage))!;
        AiSettingsPage ai = (AiSettingsPage)pageProvider.GetPage(typeof(AiSettingsPage))!;
        ComboBox themeSelector = (ComboBox)general.FindName("ThemeSelector");
        ComboBox languageSelector = (ComboBox)general.FindName("LanguageSelector");
        ComboBox voiceSelector = (ComboBox)ai.FindName("VoiceSelector");
        ComboBox inputSelector = (ComboBox)general.FindName("AudioInputSelector");
        object originalInput = inputSelector.SelectedItem;
        object originalOptions = viewModel.ThemeOptions;
        object originalFirstOption = viewModel.ThemeOptions[0];
        viewModel.Voice = SpeechVoice.Female;

        foreach (ThemePreference theme in Enum.GetValues<ThemePreference>())
        {
            viewModel.Theme = theme;

            foreach (InterfaceLanguage language in Enum.GetValues<InterfaceLanguage>())
            {
                languageSelector.SetCurrentValue(Selector.SelectedValueProperty, language);
                await Application.Current.Dispatcher.InvokeAsync(
                    () => { }, DispatcherPriority.ApplicationIdle);
                await viewModel.FlushSettingsAsync();

                if (!ReferenceEquals(originalOptions, viewModel.ThemeOptions) ||
                    !ReferenceEquals(originalFirstOption, viewModel.ThemeOptions[0]) ||
                    !Equals(themeSelector.SelectedValue, theme) ||
                    !Equals(languageSelector.SelectedValue, language) ||
                    !ReferenceEquals(originalInput, inputSelector.SelectedItem) ||
                    store.LastSaved.AudioInputDeviceId != "preview-mixer" ||
                    viewModel.Theme != theme || viewModel.Language != language ||
                    store.LastSaved.Theme != theme || store.LastSaved.Language != language)
                {
                    throw new InvalidOperationException("Language switching cleared or changed a preference.");
                }
            }
        }

        window.NavigateTo(typeof(AiSettingsPage));
        await Application.Current.Dispatcher.InvokeAsync(
            () => { }, DispatcherPriority.ApplicationIdle);

        if (!Equals(voiceSelector.SelectedValue, SpeechVoice.Female) ||
            store.LastSaved.Voice != SpeechVoice.Female)
        {
            throw new InvalidOperationException("Language switching cleared the AI voice selection.");
        }

        Console.WriteLine("PASS: language changes preserve all theme options, selection, voice, and saved preferences.");
    }

    private static async Task VerifyAudioControlsAsync(
        DesktopPageProvider pages,
        MainViewModel viewModel,
        AudioInputController controller,
        PreviewAudioBackend backend,
        PreviewSettingsStore store)
    {
        SettingsPage general = (SettingsPage)pages.GetPage(typeof(SettingsPage))!;
        ComboBox inputSelector = (ComboBox)general.FindName("AudioInputSelector");
        await viewModel.StartAudioCommand.ExecuteAsync(null);
        await SettleLayoutAsync(general);
        if (!viewModel.StopAudioCommand.CanExecute(null) || viewModel.StartAudioCommand.CanExecute(null) ||
            viewModel.LoadPreviewCommand.CanExecute(null) || viewModel.Segments.Count != 0 || viewModel.InputLevel <= 0)
        {
            throw new InvalidOperationException("Audio controls did not enter capture state or clear sample text.");
        }

        backend.SignalPeak = 0;
        await SettleLayoutAsync(general);
        await SettleLayoutAsync(general);
        if (viewModel.InputLevel != 0 || !controller.ReadSignalLevel().IsReceivingAudio)
        {
            throw new InvalidOperationException("Silent packets must leave an empty meter while still indicating received audio.");
        }

        backend.SignalPeak = null;
        await Task.Delay(TimeSpan.FromMilliseconds(1250));
        await SettleLayoutAsync(general);
        if (viewModel.InputLevel != 0 || controller.ReadSignalLevel().IsReceivingAudio)
        {
            throw new InvalidOperationException("Missing packets must expire the receiving indicator and meter.");
        }

        backend.SignalPeak = 0.5;
        await SettleLayoutAsync(general);
        if (viewModel.InputLevel <= 0)
        {
            throw new InvalidOperationException("The meter must recover when audio packets resume.");
        }

        inputSelector.SetCurrentValue(Selector.SelectedItemProperty,
            viewModel.InputDevices.Single(device => device.Id == "preview-other"));
        await SettleLayoutAsync(general);
        await viewModel.FlushSettingsAsync();
        if (controller.Snapshot.State != AudioInputState.Ready || store.LastSaved.AudioInputDeviceId != "preview-other" ||
            viewModel.InputLevel != 0)
        {
            throw new InvalidOperationException("Changing inputs must stop capture and save the explicitly selected endpoint.");
        }

        await viewModel.StartAudioCommand.ExecuteAsync(null);
        backend.Devices.RemoveAll(device => device.Id == "preview-other");
        await controller.RefreshAsync();
        await SettleLayoutAsync(general);
        if (viewModel.StartAudioCommand.CanExecute(null) || viewModel.StopAudioCommand.CanExecute(null) ||
            viewModel.SelectedInputDevice?.Id != "preview-other")
        {
            throw new InvalidOperationException("An unavailable device must stay selected without enabling capture.");
        }

        backend.Devices.Add(new("preview-other", "Sample alternate input"));
        await controller.RefreshAsync();
        await SettleLayoutAsync(general);
        if (!viewModel.StartAudioCommand.CanExecute(null) || viewModel.StopAudioCommand.CanExecute(null))
        {
            throw new InvalidOperationException("Device reconnection must require an explicit Start.");
        }

        inputSelector.SetCurrentValue(Selector.SelectedItemProperty,
            viewModel.InputDevices.Single(device => device.Id == "preview-mixer"));
        await SettleLayoutAsync(general);
        await viewModel.StartAudioCommand.ExecuteAsync(null);
        await viewModel.StopAudioCommand.ExecuteAsync(null);
        await SettleLayoutAsync(general);
        if (viewModel.InputLevel != 0)
        {
            throw new InvalidOperationException("Stop must clear the input meter.");
        }

        viewModel.LoadPreviewCommand.Execute(null);
        Console.WriteLine("PASS: fake audio verifies controls, device loss, meter signal/silence/no-packets, and clearing on Stop/input changes.");
    }

    private static void TogglePane(NavigationView navigation)
    {
        Wpf.Ui.Controls.Button toggle = (Wpf.Ui.Controls.Button)
            navigation.Template.FindName("PART_ToggleButton", navigation);
        toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, toggle));
    }

    private static async Task VerifyProviderSettingsAsync(MainWindow window, DesktopPageProvider pages,
        MainViewModel viewModel, PreviewProviderCredentialStore credentials, PreviewSettingsStore store)
    {
        window.NavigateTo(typeof(AiSettingsPage));
        AiSettingsPage page = (AiSettingsPage)pages.GetPage(typeof(AiSettingsPage))!;
        ProviderSettingsViewModel providers = viewModel.Providers;
        if (((TabView)page.FindName("AiTabs")).Style?.BasedOn is null)
        {
            throw new InvalidOperationException("AI tabs must use the WPF-UI base styles in both themes.");
        }
        ProviderConfiguration initial = providers.State;
        ServiceAssignmentViewModel stt = providers.Assignments[0];
        stt.SelectedProvider = stt.Providers.Single(profile => profile.Name == "Azure Speech");
        stt.SelectedModel = stt.Models.Single(model => model.Id == "standard-realtime");
        foreach (InterfaceLanguage language in Enum.GetValues<InterfaceLanguage>())
        {
            viewModel.Language = language;
            await SettleLayoutAsync(page);
            if (stt.SelectedProvider?.Name != "Azure Speech" || stt.SelectedModel?.Id != "standard-realtime" ||
                providers.State.Translation != initial.Translation || providers.State.SpeechGeneration != initial.SpeechGeneration)
            {
                throw new InvalidOperationException("Provider assignments must remain independent and survive localization.");
            }
        }

        ((TabView)page.FindName("AiTabs")).SelectedIndex = 1;
        await SettleLayoutAsync(page);
        providers.AddProviderCommand.Execute(null);
        providers.ProfileName = "My custom provider";
        providers.Endpoint = "https://custom.example/v1";
        providers.NewModelId = "custom-live-stt";
        providers.ModelStt = true;
        providers.AddModelCommand.Execute(null);
        Guid custom = providers.SelectedProfile!.Id;
        stt.SelectedProvider = stt.Providers.Single(profile => profile.Id == custom);
        stt.SelectedModel = stt.Models.Single();
        int beforeDelete = providers.Profiles.Count;
        await providers.DeleteProviderCommand.ExecuteAsync(null);
        if (providers.Profiles.Count != beforeDelete || string.IsNullOrWhiteSpace(providers.Status))
        {
            throw new InvalidOperationException("Deleting an assigned provider must be rejected with corrective guidance.");
        }

        ComboBox selector = (ComboBox)page.FindName("ProviderSelector");
        Wpf.Ui.Controls.PasswordBox keyEditor = (Wpf.Ui.Controls.PasswordBox)page.FindName("ApiKeyEditor");
        keyEditor.Password = "synthetic-custom-key";
        await providers.FlushCredentialsAsync();
        if (!await credentials.HasKeyAsync(custom))
        {
            throw new InvalidOperationException("The password editor did not save the API key.");
        }

        selector.SetCurrentValue(Selector.SelectedItemProperty, providers.Profiles[0]);
        await SettleLayoutAsync(page);
        if (keyEditor.Password.Length != 0)
        {
            throw new InvalidOperationException("Changing providers must clear the secret editor without reading stored secrets.");
        }

        Guid builtIn = providers.SelectedProfile!.Id;
        credentials.FailWrites = true;
        keyEditor.Password = "synthetic-built-in-key";
        await providers.FlushCredentialsAsync();
        if (string.IsNullOrWhiteSpace(providers.Status) || await credentials.HasKeyAsync(builtIn))
        {
            throw new InvalidOperationException("Credential failures must be visible and must not claim a stored key.");
        }

        credentials.FailWrites = false;
        providers.QueueApiKey("synthetic-built-in-key");
        await providers.FlushCredentialsAsync();
        await providers.RemoveKeyCommand.ExecuteAsync(null);
        selector.SetCurrentValue(Selector.SelectedItemProperty, providers.Profiles.Single(profile => profile.Id == custom));
        await SettleLayoutAsync(page);
        stt.ClearCommand.Execute(null);
        await providers.DeleteProviderCommand.ExecuteAsync(null);
        await viewModel.FlushSettingsAsync();
        if (providers.Profiles.Any(profile => profile.Id == custom) || await credentials.HasKeyAsync(custom) ||
            System.Text.Json.JsonSerializer.Serialize(store.LastSaved).Contains("synthetic-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Provider deletion must release its credential; settings must not contain API keys.");
        }

        stt.SelectedProvider = stt.Providers.Single(profile => profile.Id == initial.Stt!.ProviderId);
        stt.SelectedModel = stt.Models.Single(model => model.Id == initial.Stt!.ModelId);
        ((TabView)page.FindName("AiTabs")).SelectedIndex = 0;
        Console.WriteLine("PASS: independent provider/model assignments, localization, custom providers, protected deletion, credential editing/failures, and secret-free settings.");
    }

    private static async Task VerifyPaneToggleAsync(NavigationView navigation)
    {
        TogglePane(navigation);
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        if (navigation.IsPaneOpen)
        {
            throw new InvalidOperationException("The sidebar toggle did not collapse the pane.");
        }

        TogglePane(navigation);
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        if (!navigation.IsPaneOpen)
        {
            throw new InvalidOperationException("The sidebar toggle did not expand the pane.");
        }

        Console.WriteLine("PASS: the library toggle collapses and expands the sidebar.");
    }

    private static async Task VerifySettingsHeaderAsync(
        MainWindow window,
        NavigationView navigation,
        MainViewModel viewModel)
    {
        NavigationViewItem header = (NavigationViewItem)window.FindName("SettingsNavigationItem");
        NavigationViewItem ai = (NavigationViewItem)window.FindName("AiNavigationItem");
        window.NavigateTo(typeof(HomePage));
        header.SetCurrentValue(NavigationViewItem.IsExpandedProperty, false);

        await InvokeNavigationItemAsync(header);

        if (!header.IsExpanded || !ReferenceEquals(navigation.SelectedItem, header) ||
            !viewModel.IsSettingsPage || viewModel.IsAiSettingsPage)
        {
            throw new InvalidOperationException("Settings must expand and open General on its first click.");
        }

        await InvokeNavigationItemAsync(header);

        if (header.IsExpanded || !ReferenceEquals(navigation.SelectedItem, header) ||
            !viewModel.IsSettingsPage || viewModel.IsAiSettingsPage)
        {
            throw new InvalidOperationException("Settings must collapse while remaining on General.");
        }

        await InvokeNavigationItemAsync(header);
        await InvokeNavigationItemAsync(ai);

        if (!header.IsExpanded || !ReferenceEquals(navigation.SelectedItem, ai) ||
            !viewModel.IsAiSettingsPage)
        {
            throw new InvalidOperationException("The AI child must open its retained settings page.");
        }

        await InvokeNavigationItemAsync(header);

        if (header.IsExpanded || !ReferenceEquals(navigation.SelectedItem, header) ||
            !viewModel.IsSettingsPage || viewModel.IsAiSettingsPage || viewModel.Segments.Count != 3)
        {
            throw new InvalidOperationException("Clicking Settings from AI must open General and collapse the group.");
        }

        Console.WriteLine("PASS: Settings header clicks navigate to General and toggle expansion; AI remains its only child.");
    }

    private static async Task InvokeNavigationItemAsync(NavigationViewItem item)
    {
        // WPF-UI's automation peer exposes selection, not Invoke. This diagnostic-only
        // hook exercises the real protected click behavior, including expansion and navigation.
        MethodInfo click = typeof(NavigationViewItem).GetMethod(
            "OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The navigation control's click hook is unavailable.");
        click.Invoke(item, null);
        await Application.Current.Dispatcher.InvokeAsync(
            () => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(TimeSpan.FromMilliseconds(250));
    }

    private static async Task SettleLayoutAsync(FrameworkElement content)
    {
        await Application.Current.Dispatcher.InvokeAsync(
            () => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        content.UpdateLayout();
    }

    private sealed class PreviewSettingsStore(AppSettings settings) : ISettingsStore
    {
        public AppSettings LastSaved { get; private set; } = settings;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LastSaved);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            LastSaved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class PreviewStorageLocations : IStorageLocations
    {
        public string DataDirectory => "Preview only — user settings are not accessed";
        public string SettingsFile => string.Empty;
    }
}
