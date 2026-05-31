# WpfZpl

A **WPF (`System.Windows.Media`) rendering backend for ZPL labels**, ported from the SkiaSharp
renderer in [BinaryKits.Zpl](https://github.com/BinaryKits/BinaryKits.Zpl). It parses ZPL II with
the upstream `BinaryKits.Zpl.Viewer` analyzer and renders each label element natively in WPF —
as vector `Geometry` wherever possible — instead of SkiaSharp.

The port is validated against the original Skia renderer: a test suite renders the same ZPL through
both backends and compares the images (SSIM + pixel similarity), writing side-by-side comparison
PNGs for visual inspection.

## Why a WPF backend?

The upstream viewer renders with SkiaSharp. This project provides a drop-in equivalent for
Windows/WPF applications that want to render ZPL using the platform's own graphics stack, with three
deliberate design choices that go beyond a 1:1 port:

- **Everything is geometry.** Shapes, text, and barcodes are built as `Geometry` so they compose
  uniformly and support the Field Reverse operator faithfully.
- **Field Reverse (`^FR`)** is reproduced with `CombinedGeometry` XOR against the painted background
  (matching Skia's `SKBlendMode.Xor` / inverted-draw behaviour), rather than blending bitmaps.
- **Barcodes are vector geometry** (one rectangle per module), not rasterised bitmaps — including a
  hand-built MaxiCode (hexagons + concentric finder rings) which ZXing does not provide.

## Repository layout

```
WpfZpl/                     WPF rendering library (net10.0-windows, UseWPF)
  Rendering/               orchestrator, draw context, drawer base classes, options
  Text/                    GlyphRun-based text renderer + font manager
  ElementDrawers/          one drawer per ZPL element type
  Resources/               embedded graphic-symbol font (ZplGS.ttf)
WpfZpl.Viewer/             native WPF MVVM viewer app (ports the WebApi web UI)
WpfZpl.UnitTest/           MSTest suite: Skia-vs-WPF image comparison
  Support/                 render harness, comparer (SSIM/pixel), STA runner
  Tests/                   one test class per element category
BinaryKits.Zpl/            git submodule (fork) — see split below
WpfZpl.slnx                solution
```

`BinaryKits.Zpl` is a **git submodule**. Its `Viewer` assembly originally bundled the ZPL parser
together with the SkiaSharp renderer, which would force a Skia dependency on any consumer. It is
split into two assemblies so the WPF library stays **Skia-free**:

- **`BinaryKits.Zpl.Analyzer`** — the Skia-free parsing/analysis core (`ZplAnalyzer`, command
  analyzers, `VirtualPrinter`, `IPrinterStorage`, models, symbology encoders, helpers). `WpfZpl`
  references this.
- **`BinaryKits.Zpl.Viewer`** — the original SkiaSharp drawers, now referencing the Analyzer. Used
  only by the **test project** as the comparison reference.

So `WpfZpl` and a consuming WPF app depend only on the Skia-free Analyzer; SkiaSharp never enters
the application's dependency graph.

## Requirements

- Windows (WPF is Windows-only)
- .NET 10 SDK
- The renderer must run on an **STA thread** (a WPF requirement for `RenderTargetBitmap` /
  `GlyphTypeface`). The test harness handles this automatically.

## Getting started

```bash
git clone --recurse-submodules <repo-url>
# if you already cloned without submodules:
git submodule update --init --recursive

dotnet build WpfZpl.slnx
dotnet test  WpfZpl.UnitTest/WpfZpl.UnitTest.csproj
```

## Usage

### As a WPF control (easiest)

`ZplLabelView` (in `WpfZpl.Controls`) parses a ZPL string and renders it as vector content, with
fit-to-control scaling (`Stretch`) and whole-label `RotationAngle`:

```xml
<Window xmlns:zpl="clr-namespace:WpfZpl.Controls;assembly=WpfZpl">
    <zpl:ZplLabelView Zpl="{Binding ZplText}"
                      LabelWidth="101.6" LabelHeight="152.4" PrintDensityDpmm="8"
                      RotationAngle="0" Stretch="Uniform" OpaqueBackground="True" />
</Window>
```

It only re-parses when the content properties change; rotation/stretch are transform-only. For more
control, use the renderer directly:

### Programmatic

```csharp
using BinaryKits.Zpl.Viewer;   // parser/storage (Skia-free Analyzer assembly)
using WpfZpl.Rendering;

// 1. Parse ZPL (one storage instance is reused so ~DG/~DY downloads are
//    available to ^IM / ^XG recall elements at render time).
var storage  = new PrinterStorage();
var analyzer = new ZplAnalyzer(storage);
var elements = analyzer.Analyze(zplString).LabelInfos[0].ZplElements;

var drawer = new WpfZplElementDrawer(storage, new WpfDrawerOptions { OpaqueBackground = true });
```

The primary output is **native, scalable WPF drawing content** — no rasterisation. Coordinates are
in ZPL dots (1 dot = 1 DIU); apply a transform to scale.

```csharp
// As a reusable, freezable Drawing — e.g. bind to an Image (vector, crisp at any zoom):
DrawingGroup label = drawer.CreateDrawing(elements, 101.6, 152.4, printDensityDpmm: 8);
myImage.Source = new DrawingImage(label);

// Or draw straight into a custom control's render pass:
protected override void OnRender(DrawingContext dc) =>
    _drawer.Draw(dc, _elements, 101.6, 152.4, printDensityDpmm: 8);
```

A `DrawPng` convenience is provided for file export / image testing (it rasterises the same
`DrawingGroup` via `RenderTargetBitmap`; run on an STA thread):

```csharp
byte[] png = drawer.DrawPng(elements, 101.6, 152.4, 8);
File.WriteAllBytes("label.png", png);
```

`WpfDrawerOptions` exposes `OpaqueBackground`, `Antialias`, the `ReplaceDashWithEnDash` /
`ReplaceUnderscoreWithEnSpace` text options, a `WpfFontManager`, and a `TextBackend`
(`GlyphRun` by default; `FormattedText` available for comparison).

## Supported elements

| Category | Commands |
|---|---|
| Graphics | `^GB` box, `^GC` circle, `^GE` ellipse, `^GD` diagonal line, `^GS` symbol |
| Text | `^FD`/`^A` text fields, `^FB` field blocks (word-wrap, justification, rotation) |
| Field Reverse | `^FR` (geometry XOR), white draw |
| 1D barcodes | Code 128/39/93, ANSI Codabar, Interleaved 2of5, EAN-13, UPC-A/E, UPC extension |
| 2D barcodes | QR, Data Matrix, Aztec, PDF417, MaxiCode |
| Images | `^GF` graphic field, `^IM` image move, `^XG` recall graphic, `~DG`/`~DY` downloads |

## Viewer app

`WpfZpl.Viewer` is a small WPF MVVM application that ports the WebApi's browser UI to the desktop:
a Test/Example label browser, a ZPL editor with size/dpmm presets, whole-label rotation, a live
preview (the `ZplLabelView` control), a non-supported-commands panel, and Save PNG / Save ZPL. Run it
with `dotnet run --project WpfZpl.Viewer`.

## Test harness & fidelity

`dotnet test` renders every sample label (from `BinaryKits.Zpl.Viewer.WebApi/Labels`) through both
backends and asserts a similarity threshold. For each case it writes a three-panel
`skia | wpf | diff` PNG to **`TestOutput/RenderComparisons/`** (git-ignored) for visual review.

Typical parity against the Skia reference:

- 2D barcodes and raster images: **pixel-perfect** (module-exact geometry)
- 1D barcodes and vector shapes: **> 0.99**
- Text: **~0.96–0.99 SSIM**
- Full multi-element example labels: **~0.94–0.99**

**Note on text weight:** both backends resolve to the *same* font files, but WPF's linear-coverage
geometry fill renders text marginally heavier than Skia's gamma-corrected, hinted glyph rasteriser
at small sizes. This is inherent to the two engines (it is not a font or weight mismatch) and does
not affect SSIM; see the note in `WpfZpl/Text/WpfTextRenderer.cs`.

## License

This project depends on the upstream `BinaryKits.Zpl` (submodule); refer to its repository for its
license terms.
