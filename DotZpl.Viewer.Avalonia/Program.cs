using System;

using Avalonia;

namespace DotZpl.Viewer.Avalonia
{
    internal static class Program
    {
        // Avalonia requires the entry point to call the AppBuilder before *any* other code touches
        // Avalonia objects, so static field initialisation that uses Avalonia must not happen here.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
