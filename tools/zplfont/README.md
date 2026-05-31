# zplfont — text-source pixel fonts for ZPL

Self-sufficient toolchain (Python + fontTools) for the pixel fonts in `WpfZpl/Resources/`
that emulate the Zebra ZPL bitmap fonts. **The glyphs are authored as text** (`.pixfont`
files under `glyphs/`) and compiled to `.ttf` — no FontStruct or any GUI font editor needed.

## The source: `.pixfont`

Each font is a text file of glyphs, one ASCII-art grid per character (`#` = ink, `.` = empty),
sized to the matrix cell. Row 0 is the top of the cell; the baseline (Table 29) is fixed per
font. Comments start with `;`.

```
; ZPL Font A (cell 9x5, baseline row 7, gap 1)
font: A

U+0041 A
.###..
#...#.
#...#.
#####.
#...#.
#...#.
#...#.
......
......
```

Grids are the full **advance** width (cell width + intercharacter gap), so side bearings are
preserved. Edit pixels in any text editor; diffs are readable ("this glyph changed by a dot").

## Workflow

```bash
pip install -r requirements.txt        # fonttools (+ optional skia-pathops to merge squares)

cd tools/zplfont

# edit glyphs/font-a.pixfont, then compile to the embedded resource:
python build_font.py glyphs/font-a.pixfont A -o ../../WpfZpl/Resources/font-a.ttf
python build_font.py glyphs/font-b.pixfont B -o ../../WpfZpl/Resources/font-b.ttf
python build_font.py glyphs/font-c.pixfont C -o ../../WpfZpl/Resources/font-c.ttf
```

`build_font.py` reads the matrix for the font key from `matrices.py` (cell size, intercharacter
gap, baseline — Tables 29/30/31) and produces a TTF that renders **exactly** at the matrix when
drawn at `emSize = matrixHeight`: em = cell, monospace advance = width + gap, baseline metrics,
each `#` a 1-dot square (merged into rectangles when `skia-pathops` is installed).

**Font B** (`glyphs/font-b.pixfont`) is the bold, caps-only 11×7 font: lowercase `a-z`
alias the uppercase glyphs (Font B renders lowercase input as uppercase). It was authored
by hand the same way — to add another font, write its `glyphs/font-<x>.pixfont` and
`python build_font.py glyphs/font-<x>.pixfont <KEY> -o ../../WpfZpl/Resources/font-<x>.ttf`.

`build_font.py` sets each glyph's left side bearing to its actual `xMin` (not 0), so
centred glyphs (e.g. `!`, `I`, `T` in a caps font) keep `lsb == xMin` and round-trip
losslessly through `extract_pixels.py`.

## Scripts

| script | purpose |
|---|---|
| `build_font.py` | `.pixfont` → `.ttf` (the everyday command) |
| `pixfont.py`    | read/write the `.pixfont` format |
| `matrices.py`   | Zebra font matrices: cell H×W, gap, baseline (Tables 29/30/31) |
| `extract_pixels.py` | recover a `.pixfont` from an existing matrix-aligned `.ttf` (used once to bootstrap the source from the original fonts) |
| `normalize_fonts.py` | one-off: fix the metrics of a raw FontStruct export so `extract_pixels.py` can read it (only needed when importing a brand-new font that isn't yet on the matrix grid) |

The `.pixfont` files are the source of truth; the `.ttf`s in `Resources/` are build outputs.
