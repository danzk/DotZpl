# DotZpl

A **managed-graphics rendering backend for ZPL labels** — targeting both **WPF**
(`System.Windows.Media`) and **Avalonia** (`Avalonia.Media`) from a single multi-targeted source.
Ported from the SkiaSharp renderer in [BinaryKits.Zpl](https://github.com/BinaryKits/BinaryKits.Zpl),
it parses ZPL II with the upstream `BinaryKits.Zpl.Viewer` analyzer and renders each label element
natively — as vector `Geometry` wherever possible — instead of SkiaSharp.

The port is validated against the original Skia renderer: a test suite renders the same ZPL through
both backends and compares the images (SSIM + pixel similarity), writing side-by-side comparison
PNGs for visual inspection. A separate headless Avalonia smoke test exercises the Avalonia path.

## Why a managed-graphics backend?

The upstream viewer renders with SkiaSharp. This project provides a drop-in equivalent for
applications that want to render ZPL through the host UI framework's own graphics stack — WPF on
Windows, or Avalonia on any of its platforms — with three deliberate design choices that go beyond a
1:1 port:

- **Everything is geometry.** Shapes, text, and barcodes are built as `Geometry` so they compose
  uniformly and support the Field Reverse operator faithfully.
- **Field Reverse (`^FR`)** is reproduced with `CombinedGeometry` XOR against the painted background
  (matching Skia's `SKBlendMode.Xor` / inverted-draw behaviour), rather than blending bitmaps.
- **Barcodes are vector geometry** (one rectangle per module), not rasterised bitmaps — including a
  hand-built MaxiCode (hexagons + concentric finder rings) which ZXing does not provide.

## Repository layout

```
DotZpl/                     rendering library, multi-targeted (net10.0-windows + net10.0)
  Rendering/               orchestrator, draw context, drawer base classes, options
  Text/                    GlyphRun-based text renderer + font manager
  ElementDrawers/          one drawer per ZPL element type
  Controls/                ZplLabelView — one file per UI framework, conditionally compiled
  Resources/               embedded ZplGS + pixel fonts (font-a/b/c.ttf)
  Compat.cs                cross-framework helpers (WPF vs Avalonia API-shape differences)
DotZpl.Viewer.Shared/      MVVM view-models + platform-service interfaces (multi-targeted)
DotZpl.Viewer/             WPF viewer app (consumes Shared via WPF dispatcher/dialog impls)
DotZpl.Viewer.Avalonia/    Avalonia viewer app (consumes Shared via Avalonia impls)
DotZpl.UnitTest/           MSTest suite: Skia-vs-WPF image comparison
  Support/                 render harness, comparer (SSIM/pixel), STA runner
  Tests/                   one test class per element category
DotZpl.Avalonia.SmokeTest/ headless Avalonia render smoke tests (xUnit v3)
BinaryKits.Zpl/            git submodule (fork) — see split below
DotZpl.slnx                solution (the headless Avalonia smoke test is built separately)
```

`BinaryKits.Zpl` is a **git submodule**. Its `Viewer` assembly originally bundled the ZPL parser
together with the SkiaSharp renderer, which would force a Skia dependency on any consumer. It is
split into two assemblies so `DotZpl` stays **Skia-free**:

- **`BinaryKits.Zpl.Analyzer`** — the Skia-free parsing/analysis core (`ZplAnalyzer`, command
  analyzers, `VirtualPrinter`, `IPrinterStorage`, models, symbology encoders, helpers). `DotZpl`
  references this.
- **`BinaryKits.Zpl.Viewer`** — the original SkiaSharp drawers, now referencing the Analyzer. Used
  only by the **test project** as the comparison reference.

So `DotZpl` and a consuming app depend only on the Skia-free Analyzer; SkiaSharp never enters the
application's dependency graph.

## Requirements

- .NET 10 SDK
- **WPF target** (`net10.0-windows`): Windows only; renderer must run on an **STA thread** (a WPF
  requirement for `RenderTargetBitmap` / `GlyphTypeface`). The unit-test harness handles this.
- **Avalonia target** (`net10.0`): cross-platform; runs anywhere Avalonia 12 does.

## Getting started

```bash
git clone --recurse-submodules <repo-url>
# if you already cloned without submodules:
git submodule update --init --recursive

dotnet build DotZpl.slnx
dotnet test  DotZpl.UnitTest/DotZpl.UnitTest.csproj
# headless Avalonia smoke tests (kept outside the .slnx to avoid multi-TFM build races):
dotnet test  DotZpl.Avalonia.SmokeTest/DotZpl.Avalonia.SmokeTest.csproj
```

## Usage

### As a WPF control (easiest)

`ZplLabelView` (in `DotZpl.Controls`) parses a ZPL string and renders it as vector content, with
fit-to-control scaling (`Stretch`) and whole-label `RotationAngle`:

```xml
<Window xmlns:zpl="clr-namespace:DotZpl.Controls;assembly=DotZpl">
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
using DotZpl.Rendering;

// 1. Parse ZPL (one storage instance is reused so ~DG/~DY downloads are
//    available to ^IM / ^XG recall elements at render time).
var storage  = new PrinterStorage();
var analyzer = new ZplAnalyzer(storage);
var elements = analyzer.Analyze(zplString).LabelInfos[0].ZplElements;

var drawer = new ZplRenderer(storage, new ZplRendererOptions { OpaqueBackground = true });
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

`ZplRendererOptions` exposes `OpaqueBackground`, `Antialias`, the `ReplaceDashWithEnDash` /
`ReplaceUnderscoreWithEnSpace` text options, a `FontManager`, and a `TextBackend`
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

## Viewer apps

A small MVVM application that ports the WebApi's browser UI to the desktop: a Test/Example label
browser, a ZPL editor with size/dpmm presets, whole-label rotation, a live preview (the
`ZplLabelView` control), a non-supported-commands panel, and Save PNG / Save ZPL. Ships in two
flavours sharing the same view-models (`DotZpl.Viewer.Shared`):

- **WPF** — `dotnet run --project DotZpl.Viewer` (Windows only)
- **Avalonia** — `dotnet run --project DotZpl.Viewer.Avalonia` (cross-platform)

The shared project also defines `IDispatcher` and `IFileDialogService` so the MVVM layer doesn't
take a platform dependency; each app wires up its native implementation at startup.

## Test harness & fidelity

`dotnet test` renders every sample label (from `BinaryKits.Zpl.Viewer.WebApi/Labels`) through both
backends and asserts a similarity threshold. For each case it writes a three-panel
`skia | wpf | diff` PNG to **`TestOutput/RenderComparisons/`** (git-ignored) for visual review.

Typical parity against the Skia reference:

- QR, Data Matrix, Aztec, PDF417 and raster images: **pixel-perfect** (module-exact geometry)
- 1D barcodes and vector shapes: **> 0.99**
- Text: **~0.96–0.99 SSIM**
- MaxiCode: **~0.97 pixel / ~0.98 SSIM** — the only barcode where two aliased rasterisers
  disagree on non-axis-aligned hexagons; see `MaxiCodeElementDrawer` for the rationale
- Full multi-element example labels: **~0.94–0.99**

**Note on text weight:** both backends resolve to the *same* font files, but WPF's linear-coverage
geometry fill renders text marginally heavier than Skia's gamma-corrected, hinted glyph rasteriser
at small sizes. This is inherent to the two engines (it is not a font or weight mismatch) and does
not affect SSIM; see the note in `DotZpl/Text/TextRenderer.cs`.

## License

DotZpl is licensed under the [MIT License](LICENSE) — the same licence as the upstream
`BinaryKits.Zpl`. That dependency is consumed as a submodule and remains under its own MIT licence;
refer to its repository for its terms.
