using System.IO;
using System.Text.Json;
using CVC.RealtimeInterpreter.Core.Platforms.Windows;

namespace CVC.RealtimeInterpreter.Desktop.Diagnostics;

internal static class ProviderCredentialDiagnostics
{
    public static async Task VerifyAsync(string path)
    {
        WindowsProviderCredentialStore store = new();
        Guid id = Guid.NewGuid();
        string syntheticKey = "CVC-diagnostic-" + Guid.NewGuid().ToString("N");
        try
        {
            if (await store.HasKeyAsync(id))
            {
                throw new IOException("The isolated diagnostic credential already exists.");
            }

            await store.WriteKeyAsync(id, syntheticKey);
            bool stored = await store.HasKeyAsync(id) && await store.ReadKeyAsync(id) == syntheticKey;
            await store.WriteKeyAsync(id, syntheticKey + "-updated");
            bool updated = await store.ReadKeyAsync(id) == syntheticKey + "-updated";
            await store.DeleteKeyAsync(id);
            bool deleted = !await store.HasKeyAsync(id) && await store.ReadKeyAsync(id) is null;
            if (!stored || !updated || !deleted)
            {
                throw new IOException("Credential storage verification failed.");
            }

            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            {
                Stored = stored,
                Updated = updated,
                Deleted = deleted
            }));
        }
        finally
        {
            await store.DeleteKeyAsync(id);
        }
    }
}
