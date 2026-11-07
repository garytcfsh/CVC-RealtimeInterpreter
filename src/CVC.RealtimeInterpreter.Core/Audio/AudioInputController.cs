namespace CVC.RealtimeInterpreter.Core.Audio;

/// <summary>Owns capture lifetime. Device recovery never selects or starts another input.</summary>
public sealed class AudioInputController : IAsyncDisposable
{
    private readonly IAudioInputBackend backend;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IAudioCaptureSource? source;
    private IAudioCaptureSource? acceptingSource;
    private bool disposed;
    private long stopVersion;
    private AudioSignalMeter? signalMeter;

    public AudioInputController(IAudioInputBackend backend, string? selectedDeviceId)
    {
        this.backend = backend;
        Snapshot = new([], selectedDeviceId, AudioInputState.NoSelection);
        backend.DevicesChanged += OnDevicesChanged;
    }

    public AudioInputSnapshot Snapshot
    {
        get; private set;
    }
    public event Action<AudioInputSnapshot>? Changed;
    public event AudioDataHandler? DataAvailable;

    public AudioSignalLevel ReadSignalLevel() => Volatile.Read(ref signalMeter)?.Read() ?? AudioSignalLevel.Empty;

    public async Task RefreshAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            try
            {
                IReadOnlyList<AudioInputDevice> devices = await backend.GetDevicesAsync().ConfigureAwait(false);
                Snapshot = Snapshot with
                {
                    Devices = devices.ToArray()
                };
                if (!IsSelectedAvailable())
                {
                    await StopSourceAsync().ConfigureAwait(false);
                    Publish(SelectionState());
                }
                else if (Snapshot.State is AudioInputState.NoSelection or AudioInputState.Unavailable ||
                    Snapshot.Failure == AudioInputFailure.EnumerationFailed)
                {
                    Publish(AudioInputState.Ready);
                }
                else
                {
                    Publish(Snapshot.State, Snapshot.Failure);
                }
            }
            catch (AudioInputException exception)
            {
                await StopSourceAsync().ConfigureAwait(false);
                Publish(AudioInputState.Error, exception.Failure);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SelectDeviceAsync(string? deviceId)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || Snapshot.SelectedDeviceId == deviceId)
            {
                return;
            }

            await StopSourceAsync().ConfigureAwait(false);
            Snapshot = Snapshot with
            {
                SelectedDeviceId = deviceId
            };
            Publish(SelectionState());
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StartAsync()
    {
        long version = Volatile.Read(ref stopVersion);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || source is not null || version != Volatile.Read(ref stopVersion))
            {
                return;
            }

            if (!IsSelectedAvailable())
            {
                Publish(SelectionState());
                return;
            }

            Publish(AudioInputState.Starting);
            try
            {
                IAudioCaptureSource opened = await backend.OpenAsync(Snapshot.SelectedDeviceId!).ConfigureAwait(false);
                source = opened;
                AudioSignalMeter meter = new();
                Volatile.Write(ref signalMeter, meter);
                opened.DataAvailable += (data, format) => ForwardAudio(opened, version, meter, data, format);
                opened.Stopped += failure => OnStopped(opened, failure);
                if (version != Volatile.Read(ref stopVersion))
                {
                    await StopSourceAsync().ConfigureAwait(false);
                    Publish(SelectionState());
                    return;
                }

                Volatile.Write(ref acceptingSource, opened);
                await opened.StartAsync().ConfigureAwait(false);
                Publish(AudioInputState.Capturing);
            }
            catch (AudioInputException exception)
            {
                await StopSourceAsync().ConfigureAwait(false);
                Publish(AudioInputState.Error, exception.Failure);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync()
    {
        // Reject incoming packets immediately, even if start is still completing.
        Interlocked.Increment(ref stopVersion);
        Volatile.Write(ref acceptingSource, null);
        Volatile.Write(ref signalMeter, null);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            Publish(AudioInputState.Stopping);
            await StopSourceAsync().ConfigureAwait(false);
            Publish(SelectionState());
        }
        finally
        {
            gate.Release();
        }
    }

    private bool IsSelectedAvailable() => Snapshot.SelectedDeviceId is not null &&
        Snapshot.Devices.Any(device => device.Id == Snapshot.SelectedDeviceId);

    private AudioInputState SelectionState() => Snapshot.SelectedDeviceId is null
        ? AudioInputState.NoSelection
        : IsSelectedAvailable() ? AudioInputState.Ready : AudioInputState.Unavailable;

    private void ForwardAudio(IAudioCaptureSource sender, long version, AudioSignalMeter meter,
        ReadOnlySpan<byte> data, AudioInputFormat format)
    {
        if (version == Volatile.Read(ref stopVersion) && ReferenceEquals(Volatile.Read(ref acceptingSource), sender))
        {
            meter.Measure(data, format);
            DataAvailable?.Invoke(data, format);
        }
    }

    private async Task StopSourceAsync()
    {
        Volatile.Write(ref acceptingSource, null);
        Volatile.Write(ref signalMeter, null);
        IAudioCaptureSource? previous = source;
        source = null;
        if (previous is not null)
        {
            try
            {
                await previous.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                await previous.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void Publish(AudioInputState state, AudioInputFailure? failure = null)
    {
        Snapshot = Snapshot with
        {
            State = state,
            Failure = failure
        };
        Changed?.Invoke(Snapshot);
    }

    private void OnDevicesChanged()
    {
        // Never enumerate or release COM resources on the Windows notification callback.
        _ = Task.Run(RefreshAsync);
    }

    private void OnStopped(IAudioCaptureSource sender, AudioInputFailure? failure)
    {
        _ = HandleStoppedAsync(sender, failure);
    }

    private async Task HandleStoppedAsync(IAudioCaptureSource sender, AudioInputFailure? failure)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || !ReferenceEquals(source, sender))
            {
                return;
            }

            await StopSourceAsync().ConfigureAwait(false);
            Publish(failure == AudioInputFailure.DeviceUnavailable ? AudioInputState.Unavailable : AudioInputState.Error,
                failure ?? AudioInputFailure.CaptureFailed);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref stopVersion);
        Volatile.Write(ref acceptingSource, null);
        Volatile.Write(ref signalMeter, null);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            backend.DevicesChanged -= OnDevicesChanged;
            await StopSourceAsync().ConfigureAwait(false);
            await backend.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
