using CVC.RealtimeInterpreter.Core.Audio;
using System.Buffers.Binary;

namespace CVC.RealtimeInterpreter.Desktop.Diagnostics;

/// <summary>Deterministic UI diagnostics. Never opens an operating-system audio device.</summary>
internal sealed class PreviewAudioBackend : IAudioInputBackend
{
    public List<AudioInputDevice> Devices
    {
        get;
    } =
        [new("preview-mixer", "Sample mixer / USB audio interface (preview)"), new("preview-other", "Sample alternate input")];
    public event Action? DevicesChanged;
    public AudioInputFailure? StartFailure
    {
        get; set;
    }
    public double? SignalPeak { get; set; } = 0.25;

    public Task<IReadOnlyList<AudioInputDevice>> GetDevicesAsync() =>
        Task.FromResult<IReadOnlyList<AudioInputDevice>>(Devices.ToArray());

    public Task<IAudioCaptureSource> OpenAsync(string deviceId) =>
        Task.FromResult<IAudioCaptureSource>(new PreviewCaptureSource(StartFailure, () => SignalPeak));

    public void NotifyDevicesChanged()
    {
        DevicesChanged?.Invoke();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class PreviewCaptureSource(AudioInputFailure? startFailure, Func<double?> signalPeak) : IAudioCaptureSource
    {
        private Timer? timer;
        private readonly byte[] syntheticSamples = new byte[1600];
        public event AudioDataHandler? DataAvailable;
        public event Action<AudioInputFailure?>? Stopped;

        public Task StartAsync()
        {
            if (startFailure is not null)
            {
                throw new AudioInputException(startFailure.Value);
            }

            timer = new Timer(_ => EmitSamples(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            timer?.Dispose();
            timer = null;
            Stopped?.Invoke(null);
            return Task.CompletedTask;
        }

        private void EmitSamples()
        {
            double? peak = signalPeak();
            if (peak is null)
            {
                return;
            }

            short amplitude = (short)(peak.Value * short.MaxValue);
            for (int offset = 0; offset < syntheticSamples.Length; offset += 2)
            {
                BinaryPrimitives.WriteInt16LittleEndian(syntheticSamples.AsSpan(offset, 2), amplitude);
            }

            DataAvailable?.Invoke(syntheticSamples, new(16000, 1, 16, AudioSampleEncoding.Pcm));
        }

        public ValueTask DisposeAsync()
        {
            timer?.Dispose();
            timer = null;
            return ValueTask.CompletedTask;
        }
    }
}
