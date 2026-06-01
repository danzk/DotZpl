using System.Threading.Tasks;

using Microsoft.Win32;

using DotZpl.Viewer.Shared;

namespace DotZpl.Viewer
{
    /// <summary>WPF implementation of <see cref="IFileDialogService"/>.</summary>
    internal sealed class WpfFileDialogService : IFileDialogService
    {
        public Task<string?> SaveFileAsync(string title, string defaultFileName, string extension, string description)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                Filter = $"{description} (*.{extension})|*.{extension}|All files (*.*)|*.*",
                DefaultExt = extension,
            };
            string? path = dialog.ShowDialog() == true ? dialog.FileName : null;
            return Task.FromResult(path);
        }
    }
}
