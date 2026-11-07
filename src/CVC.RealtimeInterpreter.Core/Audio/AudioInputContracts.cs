namespace CVC.RealtimeInterpreter.Core.Audio;

public sealed record AudioInputDevice(string Id, string Name);

public enum AudioSampleEncoding
{
    Pcm,
    Float
}

public sealed record AudioInputFormat(int SampleRate, int Channels, int BitsPerSample, AudioSampleEncoding Encoding);

/// <summary>The data is valid only during the callback. Consumers must not block capture.</summary>
public delegate void AudioDataHandler(ReadOnlySpan<byte> data, AudioInputFormat format);

public enum AudioInputFailure
{
    AccessDenied,
    DeviceUnavailable,
    CaptureFailed,
    EnumerationFailed
}

public sealed class AudioInputException(AudioInputFailure failure, Exception? innerException = null)
    : Exception($"Audio input failed: {failure}.", innerException)
{
    public AudioInputFailure Failure { get; } = failure;
}

public interface IAudioCaptureSource : IAsyncDisposable
{
    event AudioDataHandler? DataAvailable;
    event Action<AudioInputFailure?>? Stopped;
    Task StartAsync();
    Task StopAsync();
}

public interface IAudioInputBackend : IAsyncDisposable
{
    event Action? DevicesChanged;
    Task<IReadOnlyList<AudioInputDevice>> GetDevicesAsync();
    Task<IAudioCaptureSource> OpenAsync(string deviceId);
}

public enum AudioInputState
{
    NoSelection,
    Ready,
    Unavailable,
    Starting,
    Capturing,
    Stopping,
    Error
}

public sealed record AudioInputSnapshot(
    IReadOnlyList<AudioInputDevice> Devices,
    string? SelectedDeviceId,
    AudioInputState State,
    AudioInputFailure? Failure = null);
