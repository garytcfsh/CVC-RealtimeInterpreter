using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Core.Audio;
using CVC.RealtimeInterpreter.Core.Transcripts;
using CVC.RealtimeInterpreter.Core.Providers;

namespace CVC.RealtimeInterpreter.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsStore settingsStore;
    private AppSettings settings;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool saveInProgress;
    private bool savePending;

    public MainViewModel(AppSettings settings, ISettingsStore settingsStore, IStorageLocations locations, string? error,
        AudioInputController? audioInput = null, IProviderCredentialStore? credentials = null)
    {
        this.settings = settings;
        this.settingsStore = settingsStore;
        DataDirectory = locations.DataDirectory;
        theme = settings.Theme;
        language = settings.Language;
        transcriptFontSize = settings.TranscriptFontSize;
        staleAudioSeconds = settings.StaleAudioSeconds;
        voice = settings.Voice;
        settingsStatus = error ?? string.Empty;
        RefreshOptionLabels();
        InitializeAudio(audioInput);
        Providers = new(settings.Providers ?? ProviderCatalog.CreateDefaults(), credentials ?? new UnavailableCredentialStore(),
            configuration =>
            {
                this.settings = this.settings with
                {
                    Providers = configuration
                };
                UpdateSettings();
            }, UseChinese);
    }

    public ProviderSettingsViewModel Providers
    {
        get;
    }

    public ObservableCollection<TranscriptSegment> Segments { get; } = [];
    public string DataDirectory
    {
        get;
    }
    public IReadOnlyList<SettingOption<ThemePreference>> ThemeOptions
    {
        get;
    } =
    [
        new(ThemePreference.System, "System"),
        new(ThemePreference.Dark, "Dark"),
        new(ThemePreference.Light, "Light")
    ];

    public IReadOnlyList<SettingOption<InterfaceLanguage>> LanguageOptions
    {
        get;
    } =
    [
        new(InterfaceLanguage.System, "Follow Windows"),
        new(InterfaceLanguage.English, "English"),
        new(InterfaceLanguage.TraditionalChinese, "繁體中文")
    ];

    public IReadOnlyList<SettingOption<SpeechVoice>> VoiceOptions
    {
        get;
    } =
    [
        new(SpeechVoice.Male, "Male"),
        new(SpeechVoice.Female, "Female")
    ];

    [ObservableProperty]
    private ThemePreference theme;

    [ObservableProperty]
    private InterfaceLanguage language;

    [ObservableProperty]
    private SpeechVoice voice;

    [ObservableProperty]
    private double transcriptFontSize;

    [ObservableProperty]
    private int staleAudioSeconds;

    [ObservableProperty]
    private string settingsStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    private bool isSettingsPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    private bool isAiSettingsPage;

    public string CurrentPageTitle => IsAiSettingsPage
        ? $"{SettingsTitle} / {AiLabel}"
        : IsSettingsPage ? $"{SettingsTitle} / {GeneralLabel}" : HomeLabel;

    public bool UseChinese => Language == InterfaceLanguage.TraditionalChinese ||
        (Language == InterfaceLanguage.System &&
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase));

    public string HomeLabel => UseChinese ? "主頁" : "Home";
    public string LiveTitle => UseChinese ? "即時口譯" : "Live interpretation";
    public string SettingsTitle => UseChinese ? "設定" : "Settings";
    public string GeneralLabel => UseChinese ? "一般" : "General";
    public string AiLabel => "AI";
    public string PreviewNotice => UseChinese
        ? "目前僅支援音訊擷取，尚未連接轉錄與翻譯。音訊不會儲存或上傳。"
        : "Audio capture only — transcription and translation are not connected yet. Audio is never saved or uploaded.";
    public string PreviewButton => UseChinese ? "載入範例文字" : "Load sample text";
    public string ClearButton => UseChinese ? "清除範例" : "Clear sample";
    public string ReturnToLiveLabel => UseChinese ? "返回最新句子" : "Return to live";
    public string StartLabel => UseChinese ? "開始" : "Start";
    public string StopLabel => UseChinese ? "停止" : "Stop";
    public string ModeLabel => UseChinese ? "模式" : "Mode";
    public string StageOneLabel => UseChinese ? "階段 1 · 僅顯示" : "Stage 1 · Display only";
    public string StageTwoLabel => UseChinese ? "階段 2 · AI 語音" : "Stage 2 · AI speech";
    public string ThemeLabel => UseChinese ? "佈景主題" : "Theme";
    public string LanguageLabel => UseChinese ? "介面語言" : "Interface language";
    public string TextSizeLabel => UseChinese ? "文字大小（兩欄同步）" : "Text size (both columns)";
    public string VoiceLabel => UseChinese ? "美式英語聲音" : "American English voice";
    public string StaleLabel => UseChinese ? "過期語音門檻（秒）" : "Stale audio threshold (seconds)";
    public string StorageLabel => UseChinese ? "資料儲存位置" : "Data location";
    public string ProviderNotice => UseChinese
        ? "可設定供應商及模型；即時 AI 服務尚未連接，不會傳送請求或產生費用。"
        : "Configure providers and models here. Live AI services are not connected yet; no requests or charges are generated.";
    public string SaveNotice => UseChinese ? "設定會自動儲存。" : "Settings save automatically.";
    public string EmptyNotice => audioSnapshot.State == AudioInputState.Capturing
        ? UseChinese ? "音訊擷取中；轉錄功能尚未連接。" : "Audio capture is active; transcription is not connected yet."
        : UseChinese ? "載入範例以檢視雙語排列。" : "Load sample text to preview the bilingual layout.";

    partial void OnThemeChanged(ThemePreference value)
    {
        UpdateSettings();
    }

    partial void OnLanguageChanged(InterfaceLanguage value)
    {
        RefreshOptionLabels();
        RefreshAudioLabels();
        Providers.SetLanguage(UseChinese);

        foreach (string name in new[]
        {
            nameof(UseChinese), nameof(CurrentPageTitle), nameof(HomeLabel), nameof(LiveTitle), nameof(SettingsTitle), nameof(PreviewNotice),
            nameof(PreviewButton), nameof(ClearButton), nameof(ReturnToLiveLabel), nameof(StartLabel),
            nameof(StopLabel), nameof(ModeLabel), nameof(StageOneLabel), nameof(StageTwoLabel),
            nameof(ThemeLabel), nameof(LanguageLabel), nameof(TextSizeLabel), nameof(VoiceLabel),
            nameof(StaleLabel), nameof(StorageLabel), nameof(ProviderNotice), nameof(SaveNotice), nameof(EmptyNotice),
            nameof(GeneralLabel), nameof(AiLabel)
        })
        {
            OnPropertyChanged(name);
        }

        UpdateSettings();
    }

    private void RefreshOptionLabels()
    {
        ThemeOptions[0].Label = UseChinese ? "跟隨系統" : "System";
        ThemeOptions[1].Label = UseChinese ? "深色" : "Dark";
        ThemeOptions[2].Label = UseChinese ? "淺色" : "Light";
        LanguageOptions[0].Label = UseChinese ? "跟隨 Windows" : "Follow Windows";
        VoiceOptions[0].Label = UseChinese ? "男聲" : "Male";
        VoiceOptions[1].Label = UseChinese ? "女聲" : "Female";
    }

    partial void OnVoiceChanged(SpeechVoice value)
    {
        UpdateSettings();
    }

    partial void OnTranscriptFontSizeChanged(double value)
    {
        UpdateSettings();
    }

    partial void OnStaleAudioSecondsChanged(int value)
    {
        UpdateSettings();
    }

    private void UpdateSettings()
    {
        settings = settings with
        {
            Theme = Theme,
            Language = Language,
            Voice = Voice,
            AudioInputDeviceId = audioSnapshot.SelectedDeviceId,
            TranscriptFontSize = TranscriptFontSize,
            StaleAudioSeconds = StaleAudioSeconds
        };
        savePending = true;
        _ = SavePendingSettingsAsync();
    }

    private async Task SavePendingSettingsAsync()
    {
        if (saveInProgress)
        {
            return;
        }

        saveInProgress = true;
        await saveGate.WaitAsync();

        try
        {
            while (savePending)
            {
                savePending = false;
                await settingsStore.SaveAsync(settings);
                SettingsStatus = string.Empty;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SettingsStatus = (UseChinese ? "無法儲存設定：" : "Could not save settings: ") + exception.Message;
        }
        finally
        {
            saveInProgress = false;
            saveGate.Release();
        }
    }

    public Task FlushSettingsAsync()
    {
        return WaitForSettingsAsync();
    }

    private async Task WaitForSettingsAsync()
    {
        await saveGate.WaitAsync();
        saveGate.Release();
    }

    [RelayCommand(CanExecute = nameof(CanLoadPreview))]
    private void LoadPreview()
    {
        Segments.Clear();
        Segments.Add(new(Guid.NewGuid(), 1, 1,
            "歡迎大家來到今天的主日禮拜。", "Welcome to today's Sunday worship service.", true));
        Segments.Add(new(Guid.NewGuid(), 2, 1,
            "今天我們一起思想如何在生活中彼此扶持。",
            "Today we reflect on how to support one another in our daily lives.", true,
            MandarinLowConfidence: true, EnglishLowConfidence: true));
        Segments.Add(new(Guid.NewGuid(), 3, 1,
            "讓我們一起禱告…", "Let us pray together…", false, IsCurrent: true));
    }

    [RelayCommand]
    private void ClearPreview()
    {
        Segments.Clear();
    }

    private sealed class UnavailableCredentialStore : IProviderCredentialStore
    {
        public Task<bool> HasKeyAsync(Guid providerId) => Task.FromResult(false);
        public Task<string?> ReadKeyAsync(Guid providerId) => Task.FromResult<string?>(null);
        public Task WriteKeyAsync(Guid providerId, string key) => Task.FromException(new IOException("Credential storage is not connected."));
        public Task DeleteKeyAsync(Guid providerId) => Task.FromException(new IOException("Credential storage is not connected."));
    }
}
