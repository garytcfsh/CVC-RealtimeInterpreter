using System.Windows;
using CVC.RealtimeInterpreter.Desktop.Composition;
using CVC.RealtimeInterpreter.Desktop.Diagnostics;

namespace CVC.RealtimeInterpreter.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && e.Args[0] == "--render-preview")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (e.Args.Length != 2)
            {
                Shutdown(2);
                return;
            }

            try
            {
                await PreviewRenderer.ExportAsync(e.Args[1]);
                Shutdown(0);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                System.Diagnostics.Trace.WriteLine(exception);
                Shutdown(1);
            }

            return;
        }

        await DesktopComposition.StartAsync(this);
    }
}
