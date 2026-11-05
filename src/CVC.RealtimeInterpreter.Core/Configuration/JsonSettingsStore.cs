using System.Text.Json;
using System.Text.Json.Serialization;

namespace CVC.RealtimeInterpreter.Core.Configuration;

/// <summary>Serializes writes and replaces settings only after a complete temporary write.</summary>
public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath = Path.GetFullPath(filePath);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!File.Exists(filePath))
            {
                return new AppSettings();
            }

            await using FileStream stream = File.OpenRead(filePath);
            AppSettings settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Settings file is empty.");

            settings.Validate();
            return settings;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
