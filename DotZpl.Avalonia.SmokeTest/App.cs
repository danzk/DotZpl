using Avalonia;
using Avalonia.Headless;

using DotZpl.Avalonia.SmokeTest;

// Tells Avalonia.Headless.XUnit how to build the app for [AvaloniaFact] tests.
[assembly: AvaloniaTestApplication(typeof(SmokeAppBuilder))]

namespace DotZpl.Avalonia.SmokeTest
{
    /// <summary>Minimal Avalonia application host for the headless render tests.</summary>
    public class App : Application
    {
    }

    public static class SmokeAppBuilder
    {
        // UseHeadlessDrawing = false + UseSkia() means RenderTargetBitmap actually rasterises through
        // Skia (CPU) instead of the no-op headless drawing — so the produced PNG has real pixels.
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
