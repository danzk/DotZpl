#!/usr/bin/env python3
"""Normalize a FontStruct pixel font so it renders at its Zebra font matrix (H x W dots).

FontStruct exports a pixel font on an integer grid, but its metrics (em size, vertical
metrics, advances) don't line up with the Zebra matrix. This tool fixes the *metrics*
so that:

  * the em square == the matrix cell: rendering at ``emSize = matrixHeight`` device
    units produces a cell exactly H dots tall (line-height = em),
  * the font is monospace at the matrix width W (real Zebra A/B/C... are fixed-cell),
  * vertical metrics keep the design's descender depth.

Glyph *outlines* are left untouched (fix shapes in FontStruct); optionally the ink can
be re-centred horizontally in the monospace cell with ``--center``.

Usage:
    python normalize_fonts.py Resources/font-a.ttf A -o Resources/font-a.ttf
    python normalize_fonts.py Resources/font-c.ttf C -o Resources/font-c.ttf
"""
import argparse
import math
import sys

from fontTools.ttLib import TTFont

from matrices import cell


def _simple_glyphs(font):
    glyf = font["glyf"]
    for name in font.getGlyphOrder():
        g = glyf[name]
        if getattr(g, "numberOfContours", 0) and g.numberOfContours > 0:
            yield name, g


def detect_grid(font, override=None) -> int:
    """The font-unit size of one pixel dot.

    Estimated as the GCD of the point coordinates of the alphanumeric glyphs (which a
    pixel font keeps strictly on the grid); falls back to unitsPerEm/16 (FontStruct's
    usual 16-dot em) if that looks implausible. Override with --grid.
    """
    if override:
        return override
    cmap = font.getBestCmap()
    glyf = font["glyf"]
    g = 0
    for ch in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789":
        name = cmap.get(ord(ch))
        if not name:
            continue
        gl = glyf[name]
        if getattr(gl, "numberOfContours", 0) and gl.numberOfContours > 0:
            for x, y in gl.coordinates:
                g = math.gcd(g, math.gcd(abs(int(x)), abs(int(y))))
    if g < 4:  # implausible pixel grid (a stray off-grid point) -> fall back
        g = font["head"].unitsPerEm // 16
    return g or 1


def vertical_span(font):
    glyf = font["glyf"]
    ymin = ymax = 0
    for _name, gl in _simple_glyphs(font):
        gl.recalcBounds(glyf)
        ymin = min(ymin, gl.yMin)
        ymax = max(ymax, gl.yMax)
    return ymin, ymax


def normalize(in_path, font_key, out_path, dpmm=8, center=False, grid_override=None):
    c = cell(font_key, dpmm)
    font = TTFont(in_path)
    glyf = font["glyf"]

    grid = detect_grid(font, grid_override)
    ymin, ymax = vertical_span(font)
    top_dots, bottom_dots = round(ymax / grid), round(ymin / grid)

    print(f"{in_path}  font {font_key}  cell {c.height}x{c.width}  gap={c.gap}  baseline={c.baseline}  grid={grid} u/dot")
    print(f"  glyph extents (baseline=0): {bottom_dots}..{top_dots} dots")
    if top_dots > c.baseline:
        print(f"  WARNING: glyphs reach {top_dots} dots above the baseline, but the cell allows {c.baseline} — fix in FontStruct.")
    if bottom_dots < -(c.height - c.baseline):
        print(f"  WARNING: descenders reach {bottom_dots} dots, deeper than the cell's {-(c.height - c.baseline)} — fix in FontStruct.")

    upm = c.height * grid                          # em == cell height
    ascent = c.baseline * grid                     # dots above baseline (Table 29)
    descent = -(c.height - c.baseline) * grid       # dots below baseline

    font["head"].unitsPerEm = upm

    hhea = font["hhea"]
    hhea.ascent, hhea.descent, hhea.lineGap = ascent, descent, 0

    os2 = font["OS/2"]
    if os2.version < 4:
        os2.version = 4                            # USE_TYPO_METRICS (bit 7) requires OS/2 v4+
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = ascent, descent, 0
    os2.usWinAscent, os2.usWinDescent = max(ascent, ymax), max(-descent, -ymin)
    os2.fsSelection |= 0x80                        # USE_TYPO_METRICS

    if c.gap is not None:
        advance = (c.width + c.gap) * grid         # monospace pitch = cell width + intercharacter gap
        hhea.advanceWidthMax = advance
        os2.xAvgCharWidth = advance
        hmtx = font["hmtx"]
        for name, gl in _simple_glyphs(font):
            gl.recalcBounds(glyf)
            ink = gl.xMax - gl.xMin
            if center and ink:
                target_lsb = round((advance - ink) / 2)
                dx = target_lsb - gl.xMin
                if dx:
                    gl.coordinates.translate((dx, 0))
                    gl.recalcBounds(glyf)
                lsb = target_lsb
            else:
                lsb = gl.xMin
            hmtx.metrics[name] = (advance, lsb)
        simple_names = {n for n, _ in _simple_glyphs(font)}
        for name in font.getGlyphOrder():          # blank glyphs (space, etc.) get the pitch too
            if name not in simple_names:
                _aw, lsb = hmtx.metrics.get(name, (0, 0))
                hmtx.metrics[name] = (advance, lsb)
        if "post" in font:
            font["post"].isFixedPitch = 1
        pitch = f"monospace pitch {c.width + c.gap}"
    else:
        pitch = "proportional (advances unchanged)"

    font.save(out_path)
    print(f"  -> {out_path}: UPM={upm}, cell={c.height}x{c.width}, ascent={c.baseline} descent={-(c.height - c.baseline)}, {pitch}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("font", help="input .ttf")
    ap.add_argument("key", help="Zebra font key: A B C D E F G H GS")
    ap.add_argument("-o", "--out", help="output .ttf (default: overwrite input)")
    ap.add_argument("--dpmm", type=int, default=8, choices=(6, 8), help="print density (default 8)")
    ap.add_argument("--grid", type=int, help="font units per dot (default: auto-detect)")
    ap.add_argument("--center", action="store_true", help="centre each glyph's ink in the monospace cell")
    args = ap.parse_args()

    try:
        normalize(args.font, args.key, args.out or args.font, args.dpmm, args.center, args.grid)
    except KeyError as e:
        sys.exit(str(e))


if __name__ == "__main__":
    main()
