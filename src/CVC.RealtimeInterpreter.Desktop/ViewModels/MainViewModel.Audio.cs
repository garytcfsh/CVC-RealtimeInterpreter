using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CVC.RealtimeInterpreter.Core.Audio;

namespace CVC.RealtimeInterpreter.Desktop.ViewModels;

public partial class MainViewModel
{
    private readonly Dispatcher audioDispatcher = Dispatcher.CurrentDispatcher;
    private AudioInputController? audioInput;
    private AudioInputSnapshot audioSnapshot = new([], null, AudioInputState.NoSelection);
    private bool updatingInputOptions;
    private bool changingInput;
    private bool closing;
    private readonly DispatcherTimer signalTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private AudioSignalLevel signalLevel = AudioSignalLevel.Empty;

    [ObservableProperty]
    private double inputLevel;

    [ObservableProperty]
    private IReadOnlyList<AudioInputDevice> inputDevices = [];

    [ObservableProperty]
    private AudioInputDevice? selectedInputDevice;

    public string InputLabel => UseChinese ? "音訊輸入" : "Audio input";
    public string RefreshInputsLabel => UseChinese ? "重新整理" : "Refresh";
    public string InputLevelLabel => UseChinese ? "輸入音量" : "Input level";
    public string InputSignalStatus => audioSnapshot.State switch
    {
        AudioInputState.Starting => UseChinese ? "等待音訊…" : "Waiting for audio…",
        AudioInputState.Capturing when !signalLevel.IsReceivingAudio => UseChinese ? "未收到音訊" : "No audio received",
        AudioInputState.Capturing when !signalLevel.IsFormatSupported => UseChinese ? "收到音訊 · 無法顯示音量" : "Receiving audio · level unavailable",
        AudioInputState.Capturing when signalLevel.Peak == 0 => UseChinese ? "收到音訊 · 靜音" : "Receiving audio · silence",
        AudioInputState.Capturing => UseChinese ? "收到音訊 · 偵測到訊號" : "Receiving audio · signal detected",
        AudioInputState.Unavailable or AudioInputState.Error => UseChinese ? "輸入無法使用" : "Input unavailable",
        _ => UseChinese ? "未擷取音訊" : "Not capturing"
    };
    public string InputHelp => UseChinese
        ? "選擇混音器所連接的 Windows 錄音裝置。切換輸入會停止擷取；準備好後請按開始。Chrome 音訊擷取尚未實作。"
        : "Select the Windows recording device connected to the mixer. Changing input stops capture; press Start when ready. Chrome capture is not implemented yet.";
    public bool CanChangeInput => !closing && !changingInput &&
        audioSnapshot.State is not (AudioInputState.Starting or AudioInputState.Stopping);
    public bool CanStartAudio => audioInput is not null && CanChangeInput &&
        audioSnapshot.State is AudioInputState.Ready or AudioInputState.Error &&
        audioSnapshot.Devices.Any(device => device.Id == audioSnapshot.SelectedDeviceId);
    public bool CanStopAudio => !closing && audioSnapshot.State is AudioInputState.Capturing or AudioInputState.Starting;
    public bool CanLoadPreview => !closing && audioSnapshot.State is not
        (AudioInputState.Capturing or AudioInputState.Starting or AudioInputState.Stopping);

    public string AudioStatus
    {
        get
        {
            string deviceName = audioSnapshot.Devices.FirstOrDefault(device => device.Id == audioSnapshot.SelectedDeviceId)?.Name ?? string.Empty;
            return audioSnapshot.Failure switch
            {
                AudioInputFailure.AccessDenied => UseChinese
                    ? "無法存取麥克風。請在 Windows 隱私權設定允許桌面應用程式使用麥克風，然後按開始重試。"
                    : "Microphone access denied. Allow desktop microphone access in Windows privacy settings, then press Start to retry.",
                AudioInputFailure.EnumerationFailed => UseChinese
                    ? "無法列出音訊裝置。請按重新整理重試。"
                    : "Could not list audio devices. Press Refresh to retry.",
                AudioInputFailure.CaptureFailed => UseChinese
                    ? "音訊擷取失敗。請檢查裝置是否被其他應用程式獨佔，然後按開始重試或選擇其他輸入。"
                    : "Audio capture failed. Check whether another app has exclusive use, then press Start to retry or select another input.",
                _ => audioSnapshot.State switch
                {
                    AudioInputState.Ready => (UseChinese ? "已就緒：" : "Ready: ") + deviceName,
                    AudioInputState.Starting => UseChinese ? "正在啟動音訊輸入…" : "Starting audio input…",
                    AudioInputState.Capturing => (UseChinese ? "正在擷取（尚無轉錄）：" : "Capturing (no transcription yet): ") + deviceName,
                    AudioInputState.Stopping => UseChinese ? "正在停止音訊輸入…" : "Stopping audio input…",
                    AudioInputState.Unavailable => UseChinese
                        ? "選定的輸入裝置無法使用。請重新連接或選擇其他裝置，再按開始。"
                        : "The selected input is unavailable. Reconnect it or select another device, then press Start.",
                    AudioInputState.NoSelection when audioInput is not null && audioSnapshot.Devices.Count == 0 => UseChinese
                        ? "找不到錄音裝置。請連接混音器的音訊介面，然後按重新整理。"
                        : "No recording devices found. Connect the mixer's audio interface, then press Refresh.",
                    _ => UseChinese ? "請在設定 → 一般選擇音訊輸入。" : "Choose an audio input in Settings → General."
                }
            };
        }
    }

    private void InitializeAudio(AudioInputController? controller)
    {
        audioInput = controller;
        signalTimer.Tick += SignalTimer_Tick;
        audioSnapshot = controller?.Snapshot ?? new([], settings.AudioInputDeviceId, AudioInputState.NoSelection);
        if (controller is not null)
        {
            controller.Changed += OnAudioChanged;
        }

        ApplyAudioSnapshot(audioSnapshot);
    }

    private void OnAudioChanged(AudioInputSnapshot snapshot)
    {
        if (audioDispatcher.CheckAccess())
        {
            ApplyAudioSnapshot(snapshot);
        }
        else
        {
            audioDispatcher.BeginInvoke(() => ApplyAudioSnapshot(snapshot));
        }
    }

    private void ApplyAudioSnapshot(AudioInputSnapshot snapshot)
    {
        if (closing)
        {
            return;
        }

        audioSnapshot = snapshot;
        updatingInputOptions = true;
        try
        {
            // Reuse options by endpoint ID. Ignore transient ComboBox nulls during list replacement.
            List<AudioInputDevice> options = snapshot.Devices.Select(device =>
                InputDevices.FirstOrDefault(existing => existing == device) ?? device).ToList();
            if (snapshot.SelectedDeviceId is not null && options.All(device => device.Id != snapshot.SelectedDeviceId))
            {
                options.Add(new(snapshot.SelectedDeviceId, UseChinese ? "選定的裝置（無法使用）" : "Selected device (unavailable)"));
            }

            InputDevices = options;
            SelectedInputDevice = options.FirstOrDefault(device => device.Id == snapshot.SelectedDeviceId);
        }
        finally
        {
            updatingInputOptions = false;
        }

        if (settings.AudioInputDeviceId != snapshot.SelectedDeviceId)
        {
            UpdateSettings();
        }

        NotifyAudioState();
    }

    private void RefreshAudioLabels()
    {
        foreach (string property in new[] { nameof(InputLabel), nameof(RefreshInputsLabel), nameof(InputHelp), nameof(InputLevelLabel) })
        {
            OnPropertyChanged(property);
        }

        ApplyAudioSnapshot(audioSnapshot);
    }

    private void NotifyAudioState()
    {
        if (!closing && audioSnapshot.State == AudioInputState.Capturing)
        {
            signalTimer.Start();
        }
        else
        {
            signalTimer.Stop();
            signalLevel = AudioSignalLevel.Empty;
            InputLevel = 0;
        }

        OnPropertyChanged(nameof(InputSignalStatus));
        OnPropertyChanged(nameof(AudioStatus));
        OnPropertyChanged(nameof(EmptyNotice));
        OnPropertyChanged(nameof(CanChangeInput));
        StartAudioCommand.NotifyCanExecuteChanged();
        StopAudioCommand.NotifyCanExecuteChanged();
        LoadPreviewCommand.NotifyCanExecuteChanged();
    }

    private void SignalTimer_Tick(object? sender, EventArgs e)
    {
        signalLevel = audioInput?.ReadSignalLevel() ?? AudioSignalLevel.Empty;
        // A logarithmic display keeps quiet signals visible; zero remains completely empty.
        InputLevel = signalLevel.Peak > 0
            ? Math.Clamp((20 * Math.Log10(signalLevel.Peak) + 60) / 60 * 100, 0, 100)
            : 0;
        OnPropertyChanged(nameof(InputSignalStatus));
    }

    partial void OnSelectedInputDeviceChanged(AudioInputDevice? value)
    {
        if (!updatingInputOptions && value is not null && audioInput is not null)
        {
            _ = ChangeInputAsync(value.Id);
        }
    }

    private async Task ChangeInputAsync(string id)
    {
        changingInput = true;
        NotifyAudioState();
        try
        {
            await audioInput!.SelectDeviceAsync(id);
        }
        finally
        {
            changingInput = false;
            NotifyAudioState();
        }
    }

    [RelayCommand]
    private async Task RefreshInputsAsync()
    {
        if (audioInput is not null)
        {
            await audioInput.RefreshAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartAudio))]
    private async Task StartAudioAsync()
    {
        Segments.Clear();
        await audioInput!.StartAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStopAudio))]
    private async Task StopAudioAsync()
    {
        await audioInput!.StopAsync();
    }

    public async Task ShutdownAsync()
    {
        closing = true;
        signalTimer.Tick -= SignalTimer_Tick;
        NotifyAudioState();
        if (audioInput is not null)
        {
            audioInput.Changed -= OnAudioChanged;
            await audioInput.DisposeAsync();
            audioSnapshot = audioInput.Snapshot;
            if (settings.AudioInputDeviceId != audioSnapshot.SelectedDeviceId)
            {
                UpdateSettings();
            }
        }

        await FlushSettingsAsync();
    }
}
