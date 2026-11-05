using System.Text.Json;
using CVC.RealtimeInterpreter.Core.Configuration;
using Xunit;

namespace CVC.RealtimeInterpreter.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CVC-Tests-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(directory, "settings.json");

    [Fact]
    public async Task Returns_defaults_without_creating_a_missing_file()
    {
        AppSettings result = await new JsonSettingsStore(SettingsPath).LoadAsync();

        Assert.Equal(new AppSettings(), result);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public async Task Round_trips_preferences_and_replaces_previous_settings()
    {
        JsonSettingsStore store = new(SettingsPath);
        await store.SaveAsync(new AppSettings());
        AppSettings expected = new()
        {
            Theme = ThemePreference.Dark,
            Language = InterfaceLanguage.TraditionalChinese,
            TranscriptFontSize = 32,
            StaleAudioSeconds = 25,
            Voice = SpeechVoice.Female
        };

        await store.SaveAsync(expected);

        Assert.Equal(expected, await store.LoadAsync());
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task Rejects_invalid_settings_without_overwriting_last_good_file()
    {
        JsonSettingsStore store = new(SettingsPath);
        await store.SaveAsync(new AppSettings());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(new AppSettings { TranscriptFontSize = double.NaN }));

        Assert.Equal(new AppSettings(), await store.LoadAsync());
    }

    [Fact]
    public async Task Reports_corrupt_data_without_replacing_it()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(SettingsPath, "{broken");

        await Assert.ThrowsAsync<JsonException>(() => new JsonSettingsStore(SettingsPath).LoadAsync());

        Assert.Equal("{broken", await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task Rejects_a_future_schema_without_silently_resetting_it()
    {
        Directory.CreateDirectory(directory);
        const string content = "{\"SchemaVersion\": 99}";
        await File.WriteAllTextAsync(SettingsPath, content);

        await Assert.ThrowsAsync<InvalidDataException>(() => new JsonSettingsStore(SettingsPath).LoadAsync());

        Assert.Equal(content, await File.ReadAllTextAsync(SettingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
