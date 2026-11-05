namespace CVC.RealtimeInterpreter.Core.Configuration;

public enum ThemePreference
{
    System,
    Dark,
    Light
}

public enum InterfaceLanguage
{
    System,
    English,
    TraditionalChinese
}

public enum SpeechVoice
{
    Male,
    Female
}

/// <summary>Portable, exportable preferences. Credentials never belong in this model.</summary>
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public ThemePreference Theme { get; init; } = ThemePreference.System;
    public InterfaceLanguage Language { get; init; } = InterfaceLanguage.System;
    public double TranscriptFontSize { get; init; } = 24;
    public int StaleAudioSeconds { get; init; } = 15;
    public SpeechVoice Voice { get; init; } = SpeechVoice.Male;

    public void Validate()
    {
        if (SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported settings schema version.");
        }

        if (!Enum.IsDefined(Theme) || !Enum.IsDefined(Language) || !Enum.IsDefined(Voice))
        {
            throw new InvalidDataException("Settings contain an unknown option.");
        }

        if (!double.IsFinite(TranscriptFontSize) || TranscriptFontSize < 14 || TranscriptFontSize > 48)
        {
            throw new InvalidDataException("Transcript text size must be between 14 and 48.");
        }

        if (StaleAudioSeconds < 1 || StaleAudioSeconds > 300)
        {
            throw new InvalidDataException("The stale audio threshold must be between 1 and 300 seconds.");
        }
    }
}
