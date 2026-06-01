using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotZpl.Viewer.Shared
{
    /// <summary>
    /// Cross-platform "open this file/URL with the OS default handler". The Avalonia viewer can run on
    /// any OS, so we pick the right launcher per platform: Windows shell-executes the path, macOS uses
    /// <c>open</c>, and Linux uses <c>xdg-open</c>.
    /// </summary>
    public static class SystemShell
    {
        /// <summary>
        /// Open <paramref name="path"/> in the system default application. Best-effort: returns
        /// <c>false</c> (rather than throwing) if no handler could be launched, so a failed open never
        /// takes down a save that already succeeded.
        /// </summary>
        public static bool OpenInDefaultApp(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // UseShellExecute lets the shell resolve the registered handler for the extension.
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo("open") { ArgumentList = { path } });
                }
                else
                {
                    Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { path } });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
