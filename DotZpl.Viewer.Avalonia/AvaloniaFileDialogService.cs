using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using DotZpl.Viewer.Shared;

namespace DotZpl.Viewer.Avalonia
{
    /// <summary>
    /// Avalonia implementation of <see cref="IFileDialogService"/>. Uses the top-level window's
    /// <c>StorageProvider</c>, which is the modern, cross-platform replacement for the old per-window
    /// dialog APIs.
    /// </summary>
    internal sealed class AvaloniaFileDialogService : IFileDialogService
    {
        private readonly Func<Window?> _window;

        public AvaloniaFileDialogService(Func<Window?> window)
        {
            _window = window;
        }

        public async Task<string?> SaveFileAsync(string title, string defaultFileName, string extension, string description)
        {
            Window? window = _window();
            if (window?.StorageProvider is not { } storage)
            {
                return null;
            }

            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName,
                DefaultExtension = extension,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new(description) { Patterns = new[] { $"*.{extension}" } },
                },
            });

            return file?.TryGetLocalPath();
        }
    }
}
