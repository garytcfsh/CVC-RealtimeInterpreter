using System.IO;
using CVC.RealtimeInterpreter.Core.Providers;

namespace CVC.RealtimeInterpreter.Desktop.Diagnostics;

internal sealed class PreviewProviderCredentialStore : IProviderCredentialStore
{
    private readonly Dictionary<Guid, string> keys = [];
    public bool FailWrites
    {
        get; set;
    }
    public Task<bool> HasKeyAsync(Guid providerId) => Task.FromResult(keys.ContainsKey(providerId));
    public Task<string?> ReadKeyAsync(Guid providerId) => Task.FromResult(keys.GetValueOrDefault(providerId));
    public Task WriteKeyAsync(Guid providerId, string key)
    {
        if (FailWrites)
        {
            throw new IOException("Synthetic credential storage failure.");
        }

        keys[providerId] = key;
        return Task.CompletedTask;
    }
    public Task DeleteKeyAsync(Guid providerId)
    {
        keys.Remove(providerId);
        return Task.CompletedTask;
    }
}
