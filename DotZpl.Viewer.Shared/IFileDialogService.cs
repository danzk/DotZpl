namespace DotZpl.Viewer.Shared
{
    /// <summary>
    /// Save-file dialog abstraction. The two platforms have very different shapes
    /// (<c>Microsoft.Win32.SaveFileDialog</c> is synchronous-on-UI; Avalonia's <c>StorageProvider</c>
    /// is async), so the shared ViewModel asks for a path via this minimal async contract.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Show a save-file dialog. Returns the chosen path, or null if the user cancelled.
        /// </summary>
        /// <param name="title">Window title.</param>
        /// <param name="defaultFileName">Pre-populated file name (no path).</param>
        /// <param name="extension">Extension without the dot, e.g. "png".</param>
        /// <param name="description">Human-readable file-type description, e.g. "PNG image".</param>
        Task<string?> SaveFileAsync(string title, string defaultFileName, string extension, string description);
    }
}
