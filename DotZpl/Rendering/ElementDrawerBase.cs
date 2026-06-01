
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;

namespace DotZpl.Rendering
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.ElementDrawers.ElementDrawerBase</c>.
    /// <see cref="CalculateNextDefaultPosition"/> is ported verbatim so default field-position
    /// chaining is identical between the Skia and WPF backends.
    /// </summary>
    public abstract class ElementDrawerBase : IElementDrawer
    {
        protected IPrinterStorage printerStorage = null!;
        protected DrawContext context = null!;

        public void Prepare(IPrinterStorage printerStorage, DrawContext context)
        {
            this.printerStorage = printerStorage;
            this.context = context;
        }

        public abstract bool CanDraw(ZplElementBase element);

        public virtual bool IsReverseDraw(ZplElementBase element) => false;

        public virtual bool IsWhiteDraw(ZplElementBase element) => false;

        public virtual bool ForceBitmapDraw(ZplElementBase element) => false;

        public virtual Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition)
            => currentPosition;

        public virtual Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont)
            => Draw(element, options, currentPosition);

        public virtual Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
            => Draw(element, options, currentPosition, internationalFont);

        protected virtual Point CalculateNextDefaultPosition(double x, double y, double elementWidth, double elementHeight, bool useFieldOrigin, FieldOrientation fieldOrientation, Point currentPosition)
        {
            if (useFieldOrigin)
            {
                switch (fieldOrientation)
                {
                    case FieldOrientation.Normal:
                        return new Point(x + elementWidth, y + elementHeight);
                    case FieldOrientation.Rotated90:
                        return new Point(x, y + elementHeight);
                    case FieldOrientation.Rotated180:
                        return new Point(x - elementWidth, y);
                    case FieldOrientation.Rotated270:
                        return new Point(x, y - elementHeight);
                }
            }
            else
            {
                switch (fieldOrientation)
                {
                    case FieldOrientation.Normal:
                        return new Point(x + elementWidth, y);
                    case FieldOrientation.Rotated90:
                        return new Point(x, y + elementWidth);
                    case FieldOrientation.Rotated180:
                        return new Point(x - elementWidth, y);
                    case FieldOrientation.Rotated270:
                        return new Point(x, y - elementWidth);
                }
            }

            return currentPosition;
        }
    }
}
