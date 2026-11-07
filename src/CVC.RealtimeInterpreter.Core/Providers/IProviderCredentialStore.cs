namespace CVC.RealtimeInterpreter.Core.Providers;

public interface IProviderCredentialStore
{
    Task<bool> HasKeyAsync(Guid providerId);
    Task<string?> ReadKeyAsync(Guid providerId);
    Task WriteKeyAsync(Guid providerId, string key);
    Task DeleteKeyAsync(Guid providerId);
}
