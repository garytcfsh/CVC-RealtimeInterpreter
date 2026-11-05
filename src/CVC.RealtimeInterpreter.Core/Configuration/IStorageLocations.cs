namespace CVC.RealtimeInterpreter.Core.Configuration;

public interface IStorageLocations
{
    string DataDirectory
    {
        get;
    }
    string SettingsFile
    {
        get;
    }
}
