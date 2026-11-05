using CVC.RealtimeInterpreter.Core.Configuration;

namespace CVC.RealtimeInterpreter.Core.Platforms.Windows;

public sealed class WindowsStorageLocations : IStorageLocations
{
    public string DataDirectory
    {
        get;
    } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "CVC-RealtimeInterpreter");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
