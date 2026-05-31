namespace WpfZpl.Viewer.ViewModels
{
    /// <summary>A sample label entry shown in the browser (mirrors the WebApi's LabelItemDto).</summary>
    public class LabelItem
    {
        public required string Name { get; init; }
        public required string Category { get; init; }   // "Test" or "Example"
        public string Format { get; init; } = string.Empty;   // e.g. "102x152"
        public required string Content { get; init; }

        public string Display => string.IsNullOrEmpty(Format) ? Name : $"{Name}  ·  {Format}";
    }
}
