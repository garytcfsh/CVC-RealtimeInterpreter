using CVC.RealtimeInterpreter.Core.Transcripts;

namespace CVC.RealtimeInterpreter.Core.Providers;

public sealed record AudioFrame(ReadOnlyMemory<byte> Data, int SampleRate, int Channels);

public sealed record TranscriptionUpdate(
    Guid SegmentId,
    int Revision,
    string Text,
    bool IsFinal,
    double? Confidence);

public sealed record TranslationResult(
    Guid SegmentId,
    int Revision,
    string Text,
    double? Confidence);

public interface ITranscriptionService
{
    IAsyncEnumerable<TranscriptionUpdate> TranscribeAsync(
        IAsyncEnumerable<AudioFrame> frames,
        CancellationToken cancellationToken = default);
}

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(
        TranscriptionUpdate source,
        CancellationToken cancellationToken = default);
}

public interface ISpeechSynthesisService
{
    Task<ReadOnlyMemory<byte>> SynthesizeAsync(
        TranscriptSegment segment,
        Configuration.SpeechVoice voice,
        CancellationToken cancellationToken = default);
}
