namespace CVC.RealtimeInterpreter.Core.Transcripts;

/// <summary>Provider-independent sentence pair. Confidence markers are set only from available evidence.</summary>
public sealed record TranscriptSegment(
    Guid Id,
    long Sequence,
    int Revision,
    string Mandarin,
    string English,
    bool IsFinal,
    bool IsCurrent = false,
    bool MandarinLowConfidence = false,
    bool EnglishLowConfidence = false);
