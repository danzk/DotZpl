// Platform namespace mapping for the WPF / Avalonia multi-target. The geometry, brush, transform,
// drawing and glyph types share the same names across both frameworks, so most files compile
// unchanged once these namespaces are imported per-platform. Genuine API-shape differences are
// isolated to a handful of files via #if WPF / #if AVALONIA and to Compat.cs helpers.
#if WPF
global using System.Windows;                 // Point, Rect, Size, Vector
global using System.Windows.Media;           // Geometry, Brush(es), transforms, GlyphRun, DrawingGroup, ...
global using System.Windows.Media.Imaging;   // RenderTargetBitmap, BitmapSource, PNG encoder
#elif AVALONIA
global using Avalonia;                        // Point, Rect, Size, Vector, Matrix, PixelSize
global using Avalonia.Media;                  // Geometry, Brush(es), transforms, GlyphRun, DrawingGroup, ...
global using Avalonia.Media.Imaging;          // RenderTargetBitmap, Bitmap

// Avalonia's raster image type is Bitmap; alias it to BitmapSource so the (shared) image-drawing code
// that refers to BitmapSource compiles on both frameworks.
global using BitmapSource = Avalonia.Media.Imaging.Bitmap;
#endif
