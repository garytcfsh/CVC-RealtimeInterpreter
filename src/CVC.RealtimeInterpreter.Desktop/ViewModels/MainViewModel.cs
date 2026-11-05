using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Core.Transcripts;

namespace CVC.RealtimeInterpreter.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsStore settingsStore;
    private AppSettings settings;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool saveInProgress;
    private bool savePending;

    public MainViewModel(AppSettings settings, ISettingsStore settingsStore, IStorageLocations locations, string? error)
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
    }

    public ObservableCollection<TranscriptSegment> Segments { get; } = [];
    public string DataDirectory
    {
        get;
    }
    public sealed record SettingOption<T>(T Value, string Label);

    public SettingOption<ThemePreference>[] ThemeOptions =>
    [
        new(ThemePreference.System, UseChinese ? "跟隨系統" : "System"),
        new(ThemePreference.Dark, UseChinese ? "深色" : "Dark"),
        new(ThemePreference.Light, UseChinese ? "淺色" : "Light")
    ];

    public SettingOption<InterfaceLanguage>[] LanguageOptions =>
    [
        new(InterfaceLanguage.System, UseChinese ? "跟隨 Windows" : "Follow Windows"),
        new(InterfaceLanguage.English, "English"),
        new(InterfaceLanguage.TraditionalChinese, "繁體中文")
    ];

    public SettingOption<SpeechVoice>[] VoiceOptions =>
    [
        new(SpeechVoice.Male, UseChinese ? "男聲" : "Male"),
        new(SpeechVoice.Female, UseChinese ? "女聲" : "Female")
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

    public bool UseChinese => Language == InterfaceLanguage.TraditionalChinese ||
        (Language == InterfaceLanguage.System &&
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase));

    public string LiveTitle => UseChinese ? "即時口譯" : "Live interpretation";
    public string SettingsTitle => UseChinese ? "設定" : "Settings";
    public string PreviewNotice => UseChinese
        ? "介面預覽 — 尚未連接音訊或 AI 服務。開始功能尚未啟用。"
        : "Interface preview — audio and AI services are not connected. Start is not available yet.";
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
        ? "AI 供應商、音訊裝置與全域快捷鍵將於後續整合。此處不會儲存 API 金鑰。"
        : "Provider, audio devices, and global shortcut integration are pending. No API key is stored here.";
    public string SaveNotice => UseChinese ? "設定會自動儲存。" : "Settings save automatically.";
    public string EmptyNotice => UseChinese ? "載入範例以檢視雙語排列。" : "Load sample text to preview the bilingual layout.";

    partial void OnThemeChanged(ThemePreference value)
    {
        UpdateSettings();
    }

    partial void OnLanguageChanged(InterfaceLanguage value)
    {
        foreach (string name in new[]
        {
            nameof(UseChinese), nameof(LiveTitle), nameof(SettingsTitle), nameof(PreviewNotice),
            nameof(PreviewButton), nameof(ClearButton), nameof(ReturnToLiveLabel), nameof(StartLabel),
            nameof(StopLabel), nameof(ModeLabel), nameof(StageOneLabel), nameof(StageTwoLabel),
            nameof(ThemeLabel), nameof(LanguageLabel), nameof(TextSizeLabel), nameof(VoiceLabel),
            nameof(StaleLabel), nameof(StorageLabel), nameof(ProviderNotice), nameof(SaveNotice), nameof(EmptyNotice),
            nameof(ThemeOptions), nameof(LanguageOptions), nameof(VoiceOptions)
        })
        {
            OnPropertyChanged(name);
        }

        UpdateSettings();
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

    [RelayCommand]
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
}
