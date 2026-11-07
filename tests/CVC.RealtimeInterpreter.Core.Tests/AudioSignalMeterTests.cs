using CVC.RealtimeInterpreter.Core.Audio;
using Xunit;

namespace CVC.RealtimeInterpreter.Core.Tests;

public sealed class AudioSignalMeterTests
{
    [Theory]
    [InlineData(AudioSampleEncoding.Pcm, 8, "8040C0")]
    [InlineData(AudioSampleEncoding.Pcm, 16, "000000C00040")]
    [InlineData(AudioSampleEncoding.Pcm, 24, "0000000000C0000040")]
    [InlineData(AudioSampleEncoding.Pcm, 32, "00000000000000C000000040")]
    [InlineData(AudioSampleEncoding.Float, 32, "00000000000000BF0000003F")]
    [InlineData(AudioSampleEncoding.Float, 64, "0000000000000000000000000000E0BF000000000000E03F")]
    public void Measures_positive_and_negative_samples_without_retaining_audio(
        AudioSampleEncoding encoding, int bits, string hex)
    {
        AudioSignalMeter meter = new();
        byte[] samples = Convert.FromHexString(hex);

        meter.Measure(samples, new(48000, 1, bits, encoding));
        Array.Clear(samples);
        AudioSignalLevel result = meter.Read();

        Assert.True(result.IsReceivingAudio);
        Assert.True(result.IsFormatSupported);
        Assert.Equal(0.5, result.Peak, precision: 6);
    }

    [Fact]
    public void Distinguishes_no_packets_from_received_silence_and_expires_old_packets()
    {
        ManualClock clock = new();
        AudioSignalMeter meter = new(clock);
        Assert.False(meter.Read().IsReceivingAudio);

        meter.Measure(new byte[4], new(16000, 1, 16, AudioSampleEncoding.Pcm));
        AudioSignalLevel silence = meter.Read();
        Assert.True(silence.IsReceivingAudio);
        Assert.Equal(0, silence.Peak);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(AudioSignalLevel.Empty, meter.Read());
    }

    [Fact]
    public void Empty_callbacks_do_not_count_as_receiving_audio()
    {
        AudioSignalMeter meter = new();
        meter.Measure([], new(16000, 1, 16, AudioSampleEncoding.Pcm));
        Assert.False(meter.Read().IsReceivingAudio);
    }

    [Fact]
    public void Shows_brief_peaks_then_falls_to_silence_and_never_reuses_an_expired_peak()
    {
        ManualClock clock = new();
        AudioSignalMeter meter = new(clock);
        AudioInputFormat format = new(16000, 1, 16, AudioSampleEncoding.Pcm);
        meter.Measure(Convert.FromHexString("0080"), format);
        meter.Measure(new byte[2], format);
        Assert.Equal(1, meter.Read().Peak);
        Assert.Equal(0, meter.Read().Peak);

        meter.Measure(Convert.FromHexString("0080"), format);
        clock.Advance(TimeSpan.FromSeconds(2));
        meter.Measure(new byte[2], format);
        Assert.Equal(0, meter.Read().Peak);
    }

    [Fact]
    public void Uses_the_loudest_channel_and_clamps_floating_point_overload()
    {
        AudioSignalMeter meter = new();
        meter.Measure(Convert.FromHexString("000000000000C03F"), new(48000, 2, 32, AudioSampleEncoding.Float));
        Assert.Equal(1, meter.Read().Peak);
    }

    [Theory]
    [InlineData(AudioSampleEncoding.Pcm, 0, 1, "0000")]
    [InlineData(AudioSampleEncoding.Pcm, 12, 1, "0000")]
    [InlineData(AudioSampleEncoding.Pcm, 16, 0, "0000")]
    [InlineData(AudioSampleEncoding.Pcm, 16, 2, "0000")]
    [InlineData(AudioSampleEncoding.Pcm, 16, 1, "00")]
    [InlineData(AudioSampleEncoding.Float, 32, 1, "0000C07F")]
    [InlineData(AudioSampleEncoding.Float, 64, 1, "000000000000F07F")]
    public void Reports_unmeasurable_packets_without_claiming_they_are_silent(
        AudioSampleEncoding encoding, int bits, int channels, string hex)
    {
        AudioSignalMeter meter = new();
        meter.Measure(Convert.FromHexString(hex), new(48000, channels, bits, encoding));

        AudioSignalLevel result = meter.Read();
        Assert.True(result.IsReceivingAudio);
        Assert.False(result.IsFormatSupported);
        Assert.Equal(0, result.Peak);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration)
        {
            timestamp += duration.Ticks;
        }
    }
}
