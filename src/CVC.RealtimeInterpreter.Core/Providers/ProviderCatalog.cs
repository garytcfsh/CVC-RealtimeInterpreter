namespace CVC.RealtimeInterpreter.Core.Providers;

/// <summary>Configuration templates, not a claim of live API integration or account availability.</summary>
public static class ProviderCatalog
{
    public static IReadOnlyList<ProviderCapability> Roles
    {
        get;
    } =
        [ProviderCapability.Stt, ProviderCapability.Translation, ProviderCapability.SpeechGeneration];

    public static ProviderCapability SupportedCapabilities(ProviderConnectionType type) => type switch
    {
        ProviderConnectionType.AzureSpeech => ProviderCapability.Stt | ProviderCapability.SpeechGeneration,
        ProviderConnectionType.OpenAi or ProviderConnectionType.OpenAiCompatible =>
            ProviderCapability.Stt | ProviderCapability.Translation | ProviderCapability.SpeechGeneration,
        _ => ProviderCapability.None
    };

    public static bool Supports(ProviderModel model, ProviderCapability role) =>
        Roles.Contains(role) && model.Capabilities.HasFlag(role) && (role != ProviderCapability.Stt || model.Streaming);

    public static ProviderModel[] ModelsFor(ProviderConnectionType type) => type switch
    {
        ProviderConnectionType.OpenAi =>
        [
            new("gpt-live-transcribe", ProviderCapability.Stt, true),
            new("gpt-4.1", ProviderCapability.Translation, true),
            new("gpt-realtime-2.1-mini", ProviderCapability.SpeechGeneration, true)
        ],
        // Azure's service-default families are profile identifiers, not deployment names.
        ProviderConnectionType.AzureSpeech =>
        [
            new("standard-realtime", ProviderCapability.Stt, true),
            new("neural-tts", ProviderCapability.SpeechGeneration, true)
        ],
        _ => []
    };

    public static string DefaultEndpoint(ProviderConnectionType type) => type == ProviderConnectionType.OpenAi
        ? "https://api.openai.com/v1" : string.Empty;

    public static ProviderConfiguration CreateDefaults() => new()
    {
        Profiles =
        [
            new(new Guid("dbba4045-b93b-4371-af02-93f1374616da"), "OpenAI", ProviderConnectionType.OpenAi,
                DefaultEndpoint(ProviderConnectionType.OpenAi), ModelsFor(ProviderConnectionType.OpenAi), true),
            new(new Guid("5cc5100c-9710-4e61-846a-c0310981f028"), "Azure Speech", ProviderConnectionType.AzureSpeech,
                string.Empty, ModelsFor(ProviderConnectionType.AzureSpeech), true)
        ]
    };
}
