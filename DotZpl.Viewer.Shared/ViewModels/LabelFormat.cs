namespace DotZpl.Viewer.Shared.ViewModels
{
    /// <summary>A label-size preset (mirrors the WebApi's label format dropdown).</summary>
    public class LabelFormat
    {
        public LabelFormat(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }

        public string Display => $"{Width} mm x {Height} mm";
    }
}
