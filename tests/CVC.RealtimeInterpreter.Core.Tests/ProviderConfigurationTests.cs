using System.Text.Json;
using CVC.RealtimeInterpreter.Core.Configuration;
using CVC.RealtimeInterpreter.Core.Providers;
using Xunit;

namespace CVC.RealtimeInterpreter.Core.Tests;

public sealed class ProviderConfigurationTests
{
    [Fact]
    public void Defaults_have_templates_without_selecting_services_or_embedding_credentials()
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        state.Validate();
        Assert.Equal(2, state.Profiles.Length);
        Assert.All(ProviderCatalog.Roles, role => Assert.Null(state.AssignmentFor(role)));
        Assert.DoesNotContain("ApiKey", JsonSerializer.Serialize(state), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolves_each_role_independently_with_shared_or_separate_connections()
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        ProviderProfile openAi = state.Profiles[0];
        ProviderProfile azure = state.Profiles[1] with
        {
            Endpoint = "https://example.cognitiveservices.azure.com"
        };
        state = state with
        {
            Profiles = [openAi, azure]
        };
        state = state.WithAssignment(ProviderCapability.Stt, new(azure.Id, "standard-realtime"))
            .WithAssignment(ProviderCapability.Translation, new(openAi.Id, "gpt-4.1"))
            .WithAssignment(ProviderCapability.SpeechGeneration, new(openAi.Id, "gpt-realtime-2.1-mini"));

        Assert.Equal(azure.Id, state.Resolve(ProviderCapability.Stt).Profile.Id);
        Assert.Equal(openAi.Id, state.Resolve(ProviderCapability.Translation).Profile.Id);
        Assert.Equal("gpt-realtime-2.1-mini", state.Resolve(ProviderCapability.SpeechGeneration).Model.Id);
        Assert.Null(state.WithAssignment(ProviderCapability.Stt, null).Stt);
        Assert.NotNull(state.WithAssignment(ProviderCapability.Stt, null).Translation);
    }

    [Fact]
    public void Renaming_preserves_stable_assignments_and_missing_confidence_is_not_a_requirement()
    {
        ProviderProfile profile = new(Guid.NewGuid(), "Custom", ProviderConnectionType.OpenAiCompatible,
            "https://example.com/v1", [new("live-mandarin", ProviderCapability.Stt, true)]);
        ProviderConfiguration state = new()
        {
            Profiles = [profile],
            Stt = new(profile.Id, "live-mandarin")
        };

        state = state with
        {
            Profiles = [profile with { Name = "Renamed" }]
        };

        Assert.Equal("Renamed", state.Resolve(ProviderCapability.Stt).Profile.Name);
        Assert.Equal(profile.Id, state.Stt!.ProviderId);
    }

    [Theory]
    [InlineData("http://remote.example.com")]
    [InlineData("https://user:secret@example.com")]
    [InlineData("https://example.com?api_key=secret")]
    [InlineData("https://example.com#secret")]
    [InlineData("file:///C:/data")]
    public void Rejects_invalid_or_secret_bearing_endpoints(string endpoint)
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        state = state with
        {
            Profiles = [state.Profiles[0] with { Endpoint = endpoint }]
        };
        Assert.Throws<InvalidDataException>(state.Validate);
    }

    [Fact]
    public void Allows_local_endpoints_and_incomplete_profiles_without_claiming_readiness()
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        ProviderProfile profile = state.Profiles[0] with
        {
            Endpoint = ""
        };
        state = state with
        {
            Profiles = [profile],
            Stt = new(profile.Id, null)
        };
        state.Validate();
        Assert.Throws<InvalidDataException>(() => state.Resolve(ProviderCapability.Stt));
        state = state with
        {
            Stt = new(profile.Id, "gpt-live-transcribe")
        };
        Assert.Throws<InvalidDataException>(() => state.Resolve(ProviderCapability.Stt));
        state = state with
        {
            Profiles = [profile with { Endpoint = "http://localhost:1234/v1" }]
        };
        Assert.NotNull(state.Resolve(ProviderCapability.Stt).Model);
    }

    [Fact]
    public void Rejects_incompatible_models_and_forged_known_capabilities()
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        ProviderProfile profile = state.Profiles[0];
        Assert.Throws<InvalidDataException>(() => (state with { Stt = new(profile.Id, "gpt-4.1") }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with
        {
            Profiles = [profile with { Models = [new("gpt-live-transcribe", ProviderCapability.Translation)] }]
        }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with
        {
            Profiles = [state.Profiles[1] with { Models = [new("translation", ProviderCapability.Translation)] }]
        }).Validate());
    }

    [Fact]
    public void Rejects_duplicate_models_identifiers_and_non_streaming_stt_assignments()
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        ProviderProfile profile = state.Profiles[0];
        Assert.Throws<InvalidDataException>(() => (state with { Profiles = [profile, profile] }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with
        {
            Profiles = [profile with { Models = [profile.Models[0], profile.Models[0]] }]
        }).Validate());
        profile = new(Guid.NewGuid(), "Batch", ProviderConnectionType.OpenAiCompatible,
            "https://example.com", [new("batch-only", ProviderCapability.Stt)]);
        Assert.Throws<InvalidDataException>(() => new ProviderConfiguration
        {
            Profiles = [profile],
            Stt = new(profile.Id, "batch-only")
        }.Validate());
    }

    [Fact]
    public void Rejects_deleting_a_referenced_profile_or_model_until_assignment_is_cleared()
    {
        ProviderConfiguration state = ProviderCatalog.CreateDefaults();
        ProviderProfile profile = state.Profiles[0];
        state = state with
        {
            Stt = new(profile.Id, "gpt-live-transcribe")
        };
        Assert.Throws<InvalidDataException>(() => (state with { Profiles = [] }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with
        {
            Profiles = [profile with { Models = profile.Models.Where(model => model.Id != "gpt-live-transcribe").ToArray() }]
        }).Validate());
        (state with
        {
            Stt = null,
            Profiles = []
        }).Validate();
    }

    [Fact]
    public async Task Persists_profiles_and_assignments_through_the_settings_store()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CVC-Providers-" + Guid.NewGuid().ToString("N"));
        try
        {
            ProviderConfiguration providers = ProviderCatalog.CreateDefaults();
            providers = providers with
            {
                Translation = new(providers.Profiles[0].Id, "gpt-4.1")
            };
            JsonSettingsStore store = new(Path.Combine(directory, "settings.json"));
            await store.SaveAsync(new AppSettings { Providers = providers });
            AppSettings reloaded = await store.LoadAsync();

            Assert.Equivalent(providers, reloaded.Providers, strict: true);
            Assert.Equal(providers.Translation, reloaded.Providers!.Translation);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
