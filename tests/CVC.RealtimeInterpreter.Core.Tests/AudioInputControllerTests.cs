using CVC.RealtimeInterpreter.Core.Audio;
using Xunit;

namespace CVC.RealtimeInterpreter.Core.Tests;

public sealed class AudioInputControllerTests
{
    [Fact]
    public async Task Does_not_select_or_open_the_default_device()
    {
        FakeBackend backend = new();
        await using AudioInputController controller = new(backend, null);

        await controller.RefreshAsync();
        await controller.StartAsync();

        Assert.Null(controller.Snapshot.SelectedDeviceId);
        Assert.Equal(AudioInputState.NoSelection, controller.Snapshot.State);
        Assert.Empty(backend.Opened);
    }

    [Fact]
    public async Task Retains_missing_device_and_requires_start_after_reconnection()
    {
        FakeBackend backend = new();
        await using AudioInputController controller = new(backend, "missing");
        await controller.RefreshAsync();
        await controller.StartAsync();
        Assert.Equal(AudioInputState.Unavailable, controller.Snapshot.State);
        Assert.Equal("missing", controller.Snapshot.SelectedDeviceId);
        Assert.Empty(backend.Opened);

        backend.Devices.Add(new("missing", "Reconnected mixer"));
        await controller.RefreshAsync();

        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
        Assert.Empty(backend.Opened);
    }

    [Fact]
    public async Task Capture_forwards_frames_until_stopped_and_ignores_late_callbacks()
    {
        FakeBackend backend = new();
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        int received = 0;
        controller.DataAvailable += (data, format) =>
        {
            Assert.Equal(2, data.Length);
            Assert.Equal(16000, format.SampleRate);
            received++;
        };
        await controller.StartAsync();
        FakeSource source = backend.Sources.Single();
        source.Emit();
        Assert.True(controller.ReadSignalLevel().IsReceivingAudio);
        Assert.Equal(AudioInputState.Capturing, controller.Snapshot.State);

        await controller.StopAsync();
        source.Emit();
        source.Fail(AudioInputFailure.CaptureFailed);

        Assert.Equal(1, received);
        Assert.False(controller.ReadSignalLevel().IsReceivingAudio);
        Assert.True(source.Disposed);
        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
    }

    [Fact]
    public async Task Device_loss_stops_capture_without_falling_back_or_restarting()
    {
        FakeBackend backend = new();
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        await controller.StartAsync();
        FakeSource original = backend.Sources.Single();

        backend.Devices.RemoveAll(device => device.Id == "mixer");
        await controller.RefreshAsync();
        Assert.True(original.Disposed);
        Assert.Equal(AudioInputState.Unavailable, controller.Snapshot.State);
        Assert.Equal("mixer", controller.Snapshot.SelectedDeviceId);

        backend.Devices.Add(new("mixer", "Mixer"));
        await controller.RefreshAsync();
        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
        Assert.Single(backend.Opened);
        await controller.StartAsync();
        Assert.Equal(2, backend.Opened.Count);
    }

    [Fact]
    public async Task Explicit_input_change_stops_old_source_and_waits_for_start()
    {
        FakeBackend backend = new();
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        await controller.StartAsync();

        await controller.SelectDeviceAsync("other");

        Assert.False(controller.ReadSignalLevel().IsReceivingAudio);
        Assert.True(backend.Sources[0].Disposed);
        Assert.Equal("other", controller.Snapshot.SelectedDeviceId);
        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
        Assert.Single(backend.Opened);
        await controller.StartAsync();
        Assert.Equal(new[] { "mixer", "other" }, backend.Opened);
        // A stale callback from the old source cannot fill the replacement's meter.
        backend.Sources[0].Emit();
        Assert.False(controller.ReadSignalLevel().IsReceivingAudio);
        backend.Sources[1].Emit();
        Assert.True(controller.ReadSignalLevel().IsReceivingAudio);
    }

    [Fact]
    public async Task Permission_failure_can_be_retried_without_changing_selection()
    {
        FakeBackend backend = new()
        {
            StartFailure = AudioInputFailure.AccessDenied
        };
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        await controller.StartAsync();
        Assert.Equal(AudioInputState.Error, controller.Snapshot.State);
        Assert.Equal(AudioInputFailure.AccessDenied, controller.Snapshot.Failure);
        Assert.True(backend.Sources[0].Disposed);

        backend.StartFailure = null;
        await controller.StartAsync();

        Assert.Equal(AudioInputState.Capturing, controller.Snapshot.State);
        Assert.Equal("mixer", controller.Snapshot.SelectedDeviceId);
    }

    [Fact]
    public async Task Stop_during_open_prevents_capture_from_starting()
    {
        FakeBackend backend = new()
        {
            Opening = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        Task starting = controller.StartAsync();
        await backend.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task stopping = controller.StopAsync();
        backend.Opening.SetResult();

        await Task.WhenAll(starting, stopping).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(backend.Sources[0].Started);
        Assert.True(backend.Sources[0].Disposed);
        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
    }

    [Fact]
    public async Task Unexpected_stop_reports_failure_and_does_not_restart()
    {
        FakeBackend backend = new();
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        await controller.StartAsync();
        TaskCompletionSource<AudioInputSnapshot> failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.Changed += snapshot =>
        {
            if (snapshot.State == AudioInputState.Error)
            {
                failed.TrySetResult(snapshot);
            }
        };

        backend.Sources[0].Fail(AudioInputFailure.CaptureFailed);
        AudioInputSnapshot result = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(AudioInputFailure.CaptureFailed, result.Failure);
        Assert.True(backend.Sources[0].Disposed);
        Assert.Single(backend.Opened);
    }

    [Fact]
    public async Task Stop_during_start_rejects_packets_before_start_completes()
    {
        TaskCompletionSource startGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeBackend backend = new()
        {
            Starting = startGate
        };
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        int received = 0;
        controller.DataAvailable += (_, _) => received++;
        Task starting = controller.StartAsync();
        FakeSource source = backend.Sources.Single();
        await source.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task stopping = controller.StopAsync();
        source.Emit();
        startGate.SetResult();
        await Task.WhenAll(starting, stopping).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, received);
        Assert.True(source.Disposed);
        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
    }

    [Fact]
    public async Task Enumeration_failure_is_reported_and_refresh_can_recover()
    {
        FakeBackend backend = new()
        {
            EnumerationFails = true
        };
        await using AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        Assert.Equal(AudioInputFailure.EnumerationFailed, controller.Snapshot.Failure);

        backend.EnumerationFails = false;
        await controller.RefreshAsync();

        Assert.Equal(AudioInputState.Ready, controller.Snapshot.State);
        Assert.Empty(backend.Opened);
    }

    [Fact]
    public async Task Notification_refreshes_devices_and_shutdown_releases_active_capture()
    {
        FakeBackend backend = new();
        AudioInputController controller = new(backend, "mixer");
        await controller.RefreshAsync();
        await controller.StartAsync();
        backend.Devices.RemoveAll(device => device.Id == "mixer");
        TaskCompletionSource unavailable = new(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.Changed += snapshot =>
        {
            if (snapshot.State == AudioInputState.Unavailable)
            {
                unavailable.TrySetResult();
            }
        };
        backend.Notify();
        await unavailable.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await controller.DisposeAsync();
        backend.Notify();
        await controller.StartAsync();
        await controller.DisposeAsync();

        Assert.True(backend.Disposed);
        Assert.True(backend.Sources[0].Disposed);
        Assert.Single(backend.Opened);
    }

    private sealed class FakeBackend : IAudioInputBackend
    {
        public List<AudioInputDevice> Devices { get; } = [new("mixer", "Mixer"), new("other", "Other input")];
        public List<string> Opened { get; } = [];
        public List<FakeSource> Sources { get; } = [];
        public AudioInputFailure? StartFailure
        {
            get; set;
        }
        public bool EnumerationFails
        {
            get; set;
        }
        public bool Disposed
        {
            get; private set;
        }
        public TaskCompletionSource? Opening
        {
            get; init;
        }
        public TaskCompletionSource? Starting
        {
            get; init;
        }
        public TaskCompletionSource OpenEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action? DevicesChanged;

        public Task<IReadOnlyList<AudioInputDevice>> GetDevicesAsync()
        {
            if (EnumerationFails)
            {
                throw new AudioInputException(AudioInputFailure.EnumerationFailed);
            }

            return Task.FromResult<IReadOnlyList<AudioInputDevice>>(Devices.ToArray());
        }

        public async Task<IAudioCaptureSource> OpenAsync(string deviceId)
        {
            Opened.Add(deviceId);
            OpenEntered.TrySetResult();
            if (Opening is not null)
            {
                await Opening.Task;
            }

            FakeSource source = new(StartFailure, Starting);
            Sources.Add(source);
            return source;
        }

        public void Notify()
        {
            DevicesChanged?.Invoke();
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSource(AudioInputFailure? failure, TaskCompletionSource? starting) : IAudioCaptureSource
    {
        public bool Started
        {
            get; private set;
        }
        public bool Disposed
        {
            get; private set;
        }
        public event AudioDataHandler? DataAvailable;
        public event Action<AudioInputFailure?>? Stopped;
        public TaskCompletionSource StartEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StartAsync()
        {
            if (failure is not null)
            {
                throw new AudioInputException(failure.Value);
            }

            Started = true;
            StartEntered.TrySetResult();
            if (starting is not null)
            {
                await starting.Task;
            }
        }

        public void Emit()
        {
            DataAvailable?.Invoke(new byte[2], new(16000, 1, 16, AudioSampleEncoding.Pcm));
        }

        public void Fail(AudioInputFailure reason)
        {
            Stopped?.Invoke(reason);
        }

        public Task StopAsync()
        {
            Stopped?.Invoke(null);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
