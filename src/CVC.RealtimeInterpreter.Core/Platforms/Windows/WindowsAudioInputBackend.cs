using System.Runtime.InteropServices;
using CVC.RealtimeInterpreter.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CVC.RealtimeInterpreter.Core.Platforms.Windows;

/// <summary>Windows endpoints only; never follows the default device or writes audio to disk.</summary>
public sealed class WindowsAudioInputBackend : IAudioInputBackend
{
    private MMDeviceEnumerator? notificationsEnumerator;
    private MMDeviceNotificationClient? notifications;

    public event Action? DevicesChanged;

    public Task<IReadOnlyList<AudioInputDevice>> GetDevicesAsync() => Task.Run<IReadOnlyList<AudioInputDevice>>(() =>
    {
        try
        {
            EnsureNotifications();
            using MMDeviceEnumerator enumerator = new();
            using MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            List<AudioInputDevice> results = [];
            foreach (MMDevice device in devices)
            {
                using (device)
                {
                    results.Add(new(device.ID, device.FriendlyName));
                }
            }

            return results.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        catch (Exception exception) when (IsAudioException(exception))
        {
            throw new AudioInputException(AudioInputFailure.EnumerationFailed, exception);
        }
    });

    private void EnsureNotifications()
    {
        if (notifications is not null)
        {
            return;
        }

        notificationsEnumerator ??= new MMDeviceEnumerator();
        notifications = notificationsEnumerator.CreateNotificationClient(useSynchronizationContext: false);
        notifications.DeviceAdded += (_, _) => DevicesChanged?.Invoke();
        notifications.DeviceRemoved += (_, _) => DevicesChanged?.Invoke();
        notifications.DeviceStateChanged += (_, _) => DevicesChanged?.Invoke();
    }

    public Task<IAudioCaptureSource> OpenAsync(string deviceId) => Task.Run<IAudioCaptureSource>(() =>
    {
        MMDevice? device = null;
        WasapiRecorder? recorder = null;
        try
        {
            using MMDeviceEnumerator enumerator = new();
            device = enumerator.GetDevice(deviceId);
            if (device.State != DeviceState.Active || device.DataFlow != DataFlow.Capture)
            {
                throw new AudioInputException(AudioInputFailure.DeviceUnavailable);
            }

            recorder = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithBufferLength(50)
                .Build();
            return new WindowsCaptureSource(device, recorder);
        }
        catch (Exception exception)
        {
            recorder?.Dispose();
            device?.Dispose();
            if (exception is AudioInputException)
            {
                throw;
            }

            if (IsAudioException(exception))
            {
                throw new AudioInputException(Classify(exception), exception);
            }

            throw;
        }
    });

    internal static bool IsAudioException(Exception exception) =>
        exception is COMException or CoreAudioException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException;

    internal static AudioInputFailure Classify(Exception exception) => exception.HResult switch
    {
        unchecked((int)0x80070005) => AudioInputFailure.AccessDenied,
        unchecked((int)0x88890004) or unchecked((int)0x88890026) or unchecked((int)0x80070490) => AudioInputFailure.DeviceUnavailable,
        _ => exception is UnauthorizedAccessException ? AudioInputFailure.AccessDenied : AudioInputFailure.CaptureFailed
    };

    public ValueTask DisposeAsync()
    {
        notifications?.Dispose();
        notifications = null;
        notificationsEnumerator?.Dispose();
        notificationsEnumerator = null;
        return ValueTask.CompletedTask;
    }

    private sealed class WindowsCaptureSource : IAudioCaptureSource
    {
        private readonly MMDevice device;
        private readonly WasapiRecorder recorder;
        private readonly AudioInputFormat format;

        public WindowsCaptureSource(MMDevice device, WasapiRecorder recorder)
        {
            this.device = device;
            this.recorder = recorder;
            WaveFormat waveFormat = recorder.WaveFormat;
            bool floatingPoint = waveFormat.Encoding == WaveFormatEncoding.IeeeFloat ||
                waveFormat is WaveFormatExtensible extensible &&
                extensible.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
            bool pcm = waveFormat.Encoding == WaveFormatEncoding.Pcm ||
                waveFormat is WaveFormatExtensible pcmExtensible &&
                pcmExtensible.SubFormat == new Guid("00000001-0000-0010-8000-00aa00389b71");
            if (!floatingPoint && !pcm)
            {
                throw new NotSupportedException("The selected audio input does not provide PCM or floating-point samples.");
            }

            format = new(waveFormat.SampleRate, waveFormat.Channels, waveFormat.BitsPerSample,
                floatingPoint ? AudioSampleEncoding.Float : AudioSampleEncoding.Pcm);
            recorder.DataAvailable += OnDataAvailable;
            recorder.RecordingStopped += OnRecordingStopped;
        }

        public event AudioDataHandler? DataAvailable;
        public event Action<AudioInputFailure?>? Stopped;

        private void OnDataAvailable(ReadOnlySpan<byte> data, AudioClientBufferFlags flags, long position, long timestamp)
        {
            DataAvailable?.Invoke(data, format);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs args)
        {
            Stopped?.Invoke(args.Exception is null ? null : Classify(args.Exception));
        }

        public Task StartAsync() => Task.Run(() =>
        {
            try
            {
                recorder.StartRecording();
            }
            catch (Exception exception) when (IsAudioException(exception))
            {
                throw new AudioInputException(Classify(exception), exception);
            }
        });

        public Task StopAsync()
        {
            recorder.StopRecording();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            recorder.DataAvailable -= OnDataAvailable;
            recorder.RecordingStopped -= OnRecordingStopped;
            try
            {
                await recorder.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                device.Dispose();
            }
        }
    }
}
