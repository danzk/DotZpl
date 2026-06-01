
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer;

namespace DotZpl.Rendering
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.ElementDrawers.IElementDrawer</c>.
    /// <c>SKCanvas</c> becomes <see cref="DrawContext"/> and <c>SKPoint</c> becomes <see cref="Point"/>.
    /// </summary>
    public interface IElementDrawer
    {
        /// <summary>Prepare the drawer with the printer storage and the geometry-accumulating context.</summary>
        void Prepare(IPrinterStorage printerStorage, DrawContext context);

        /// <summary>Check if the drawer can draw this element.</summary>
        bool CanDraw(ZplElementBase element);

        /// <summary>Element requires reverse (XOR) draw (<c>^FR</c>).</summary>
        bool IsReverseDraw(ZplElementBase element);

        /// <summary>Element is drawn white (inverted-color reverse).</summary>
        bool IsWhiteDraw(ZplElementBase element);

        /// <summary>Element needs to be drawn in bitmap mode (kept for API symmetry).</summary>
        bool ForceBitmapDraw(ZplElementBase element);

        /// <summary>Draw the element; returns the updated default field position.</summary>
        Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition);

        /// <summary>Draw the element with international font context.</summary>
        Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont);

        /// <summary>Draw the element with international font and print-density context.</summary>
        Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm);
    }
}
