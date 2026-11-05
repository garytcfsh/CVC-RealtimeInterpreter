using System.Windows;
using CVC.RealtimeInterpreter.Desktop.Composition;

namespace CVC.RealtimeInterpreter.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await DesktopComposition.StartAsync(this);
    }
}
