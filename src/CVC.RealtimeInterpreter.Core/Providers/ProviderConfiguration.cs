namespace CVC.RealtimeInterpreter.Core.Providers;

[Flags]
public enum ProviderCapability
{
    None = 0,
    Stt = 1,
    Translation = 2,
    SpeechGeneration = 4
}

public enum ProviderConnectionType
{
    OpenAi,
    AzureSpeech,
    OpenAiCompatible
}

public sealed record ProviderModel(string Id, ProviderCapability Capabilities, bool Streaming = false);

/// <summary>Exportable connection configuration. API keys live in the credential store.</summary>
public sealed record ProviderProfile(
    Guid Id,
    string Name,
    ProviderConnectionType ConnectionType,
    string Endpoint,
    ProviderModel[] Models,
    bool IsBuiltIn = false)
{
    public ProviderCapability Capabilities => Models.Aggregate(ProviderCapability.None,
        (capabilities, model) => capabilities | model.Capabilities);
}

public sealed record ServiceAssignment(Guid ProviderId, string? ModelId);

public sealed record ProviderConfiguration
{
    public ProviderProfile[] Profiles { get; init; } = [];
    public ServiceAssignment? Stt
    {
        get; init;
    }
    public ServiceAssignment? Translation
    {
        get; init;
    }
    public ServiceAssignment? SpeechGeneration
    {
        get; init;
    }

    public ServiceAssignment? AssignmentFor(ProviderCapability role) => role switch
    {
        ProviderCapability.Stt => Stt,
        ProviderCapability.Translation => Translation,
        ProviderCapability.SpeechGeneration => SpeechGeneration,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public ProviderConfiguration WithAssignment(ProviderCapability role, ServiceAssignment? assignment) => role switch
    {
        ProviderCapability.Stt => this with { Stt = assignment },
        ProviderCapability.Translation => this with { Translation = assignment },
        ProviderCapability.SpeechGeneration => this with { SpeechGeneration = assignment },
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public void Validate()
    {
        if (Profiles is null || Profiles.Any(profile => profile is null) ||
            Profiles.Select(profile => profile.Id).Distinct().Count() != Profiles.Length)
        {
            throw new InvalidDataException("Provider identifiers must be unique.");
        }

        foreach (ProviderProfile profile in Profiles)
        {
            if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 100 ||
                !Enum.IsDefined(profile.ConnectionType) || profile.Models is null)
            {
                throw new InvalidDataException("A provider needs a name and supported connection type.");
            }

            // Blank endpoints are allowed while a new profile is being configured.
            if (profile.Endpoint is null || profile.Endpoint.Length > 2048 ||
                profile.Endpoint.Length > 0 && (!Uri.TryCreate(profile.Endpoint, UriKind.Absolute, out Uri? uri) ||
                !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
            {
                throw new InvalidDataException("Use an HTTPS endpoint (or local HTTP) without credentials, query parameters, or fragments.");
            }

            if (profile.Models.Any(model => model is null || string.IsNullOrWhiteSpace(model.Id) || model.Id.Length > 200 ||
                model.Capabilities == ProviderCapability.None ||
                (model.Capabilities & ~ProviderCatalog.SupportedCapabilities(profile.ConnectionType)) != 0) ||
                profile.Models.Select(model => model.Id).Distinct(StringComparer.Ordinal).Count() != profile.Models.Length)
            {
                throw new InvalidDataException("Models need unique identifiers and capabilities supported by the connection type.");
            }

            foreach (ProviderModel model in profile.Models)
            {
                ProviderModel? known = ProviderCatalog.ModelsFor(profile.ConnectionType).FirstOrDefault(item => item.Id == model.Id);
                if (known is not null && known != model)
                {
                    throw new InvalidDataException("Known model capabilities must match the built-in catalog.");
                }
            }
        }

        foreach (ProviderCapability role in ProviderCatalog.Roles)
        {
            ServiceAssignment? assignment = AssignmentFor(role);
            if (assignment is null)
            {
                continue;
            }

            ProviderProfile? profile = Profiles.FirstOrDefault(item => item.Id == assignment.ProviderId);
            if (profile is null || !profile.Models.Any(model => ProviderCatalog.Supports(model, role)))
            {
                throw new InvalidDataException("Clear service assignments before removing their provider or required capability.");
            }

            if (assignment.ModelId is not null && !profile.Models.Any(model =>
                model.Id == assignment.ModelId && ProviderCatalog.Supports(model, role)))
            {
                throw new InvalidDataException("The assigned model does not support this service. Clear its assignment first.");
            }
        }
    }

    public (ProviderProfile Profile, ProviderModel Model) Resolve(ProviderCapability role)
    {
        Validate();
        ServiceAssignment assignment = AssignmentFor(role) ?? throw new InvalidDataException("Choose a provider for this service.");
        ProviderProfile profile = Profiles.Single(item => item.Id == assignment.ProviderId);
        ProviderModel model = profile.Models.FirstOrDefault(item => item.Id == assignment.ModelId)
            ?? throw new InvalidDataException("Choose a model for this service.");
        if (string.IsNullOrWhiteSpace(profile.Endpoint))
        {
            throw new InvalidDataException("Set the selected provider's endpoint.");
        }

        return (profile, model);
    }
}
