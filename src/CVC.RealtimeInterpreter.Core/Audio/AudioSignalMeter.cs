using System.Buffers.Binary;

namespace CVC.RealtimeInterpreter.Core.Audio;

public sealed record AudioSignalLevel(bool IsReceivingAudio, double Peak, bool IsFormatSupported)
{
    public static AudioSignalLevel Empty { get; } = new(false, 0, true);
}

/// <summary>Retains only level measurements and arrival time, never sample buffers.</summary>
public sealed class AudioSignalMeter(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly object sync = new();
    private long? lastPacket;
    private double pendingPeak;
    private double latestPeak;
    private bool hasPending;
    private bool supported = true;

    public void Measure(ReadOnlySpan<byte> data, AudioInputFormat format)
    {
        if (data.IsEmpty)
        {
            return;
        }

        double peak = 0;
        int bytesPerSample = format.BitsPerSample / 8;
        bool valid = format.SampleRate > 0 && format.Channels > 0 &&
            (format.Encoding == AudioSampleEncoding.Pcm && format.BitsPerSample is 8 or 16 or 24 or 32 ||
            format.Encoding == AudioSampleEncoding.Float && format.BitsPerSample is 32 or 64);
        valid = valid && data.Length % bytesPerSample == 0 &&
            data.Length / bytesPerSample % format.Channels == 0;

        if (valid)
        {
            for (int offset = 0; offset < data.Length; offset += bytesPerSample)
            {
                ReadOnlySpan<byte> sample = data.Slice(offset, bytesPerSample);
                double value;
                if (format.Encoding == AudioSampleEncoding.Float)
                {
                    value = bytesPerSample == 4
                        ? BinaryPrimitives.ReadSingleLittleEndian(sample)
                        : BinaryPrimitives.ReadDoubleLittleEndian(sample);
                }
                else
                {
                    value = bytesPerSample switch
                    {
                        1 => (sample[0] - 128) / 128d,
                        2 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768d,
                        3 => ((sample[0] | sample[1] << 8 | sample[2] << 16) << 8 >> 8) / 8388608d,
                        _ => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648d
                    };
                }

                if (!double.IsFinite(value))
                {
                    valid = false;
                    break;
                }

                peak = Math.Max(peak, Math.Min(1, Math.Abs(value)));
            }
        }

        lock (sync)
        {
            if (lastPacket is not null && clock.GetElapsedTime(lastPacket.Value) >= TimeSpan.FromSeconds(1))
            {
                pendingPeak = 0;
            }

            lastPacket = clock.GetTimestamp();
            supported = valid;
            latestPeak = valid ? peak : 0;
            pendingPeak = valid ? Math.Max(pendingPeak, peak) : 0;
            hasPending = true;
        }
    }

    /// <summary>Consumes the peak since the last read. Packet arrival expires after one second.</summary>
    public AudioSignalLevel Read()
    {
        lock (sync)
        {
            if (lastPacket is null || clock.GetElapsedTime(lastPacket.Value) >= TimeSpan.FromSeconds(1))
            {
                pendingPeak = 0;
                hasPending = false;
                return AudioSignalLevel.Empty;
            }

            double peak = hasPending ? pendingPeak : latestPeak;
            pendingPeak = 0;
            hasPending = false;
            return new(true, supported ? peak : 0, supported);
        }
    }
}
