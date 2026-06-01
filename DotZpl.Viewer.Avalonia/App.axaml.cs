using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using DotZpl.Viewer.Shared.ViewModels;

namespace DotZpl.Viewer.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                // The file-dialog service captures the window late-bound: by the time the user clicks
                // Save the window has been shown and the StorageProvider is live, so we don't need to
                // wait on any initialisation here.
                window.DataContext = new MainViewModel(
                    new AvaloniaDispatcher(),
                    new AvaloniaFileDialogService(() => window));
                desktop.MainWindow = window;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
