using System.IO;
using System.Text.Json;
using CVC.RealtimeInterpreter.Core.Platforms.Windows;

namespace CVC.RealtimeInterpreter.Desktop.Diagnostics;

internal static class AudioInputDiagnostics
{
    /// <summary>Enumerates endpoints and releases notification resources without capturing audio.</summary>
    public static async Task ExportDevicesAsync(string path)
    {
        await using WindowsAudioInputBackend backend = new();
        var devices = await backend.GetDevicesAsync();
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(devices,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
