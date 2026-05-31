#!/usr/bin/env python3
"""Recover a .pixfont source from an existing (normalized) ZPL pixel font.

Samples each glyph's outline at the centre of every dot in the matrix cell, so the
binary font can be turned back into editable text. Pixels outside the matrix cell are
clipped (and reported) — the cell, per the matrix, is authoritative.

Usage:
    python extract_pixels.py ../../WpfZpl/Resources/font-a.ttf A -o glyphs/font-a.pixfont
"""
import argparse
import os
import sys

from fontTools.ttLib import TTFont
from fontTools.pens.pointInsidePen import PointInsidePen

import pixfont
from matrices import cell


def sample_glyph(glyph_set, name, height, cols, baseline, grid):
    """Return the (height x cols) ON/OFF grid (row 0 = top of cell, cols = the advance width
    so glyph side-bearings are preserved), plus whether ink fell outside the sampled box."""
    glyph = glyph_set[name]

    def inked(cx, cy):
        pen = PointInsidePen(glyph_set, (cx, cy))
        glyph.draw(pen)
        return pen.getResult()

    rows = []
    for r in range(height):
        cy = (baseline - r) * grid - grid / 2          # dot-cell centre y
        rows.append("".join(pixfont.ON if inked(c * grid + grid / 2, cy) else pixfont.OFF
                            for c in range(cols)))

    # check just outside the sampled box (above the cell top, below the cell bottom, past the advance)
    clipped = any(inked(c * grid + grid / 2, (baseline + 1) * grid - grid / 2) for c in range(cols)) \
        or any(inked(c * grid + grid / 2, (baseline - height) * grid - grid / 2) for c in range(cols)) \
        or any(inked(cols * grid + grid / 2, (baseline - r) * grid - grid / 2) for r in range(height))
    return rows, clipped


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("font", help="input .ttf")
    ap.add_argument("key", help="Zebra font key (A B C D E F G H GS)")
    ap.add_argument("-o", "--out", help="output .pixfont (default: glyphs/<font>.pixfont)")
    ap.add_argument("--dpmm", type=int, default=8, choices=(6, 8))
    args = ap.parse_args()

    c = cell(args.key, args.dpmm)
    font = TTFont(args.font)
    grid = font["head"].unitsPerEm // c.height        # font units per dot
    cols = c.width + (c.gap or 0)                      # sample the whole advance (keeps side bearings)
    glyph_set = font.getGlyphSet()
    cmap = font.getBestCmap()

    glyphs, clipped_names = [], []
    for cp in sorted(cmap):
        name = cmap[cp]
        rows, clipped = sample_glyph(glyph_set, name, c.height, cols, c.baseline, grid)
        glyphs.append(pixfont.PixGlyph(cp, name, rows))
        if clipped:
            clipped_names.append(name)

    out = args.out or os.path.join("glyphs", f"{os.path.splitext(os.path.basename(args.font))[0]}.pixfont")
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    pixfont.write(out, args.key, glyphs,
                  comment=f"ZPL Font {args.key} (cell {c.height}x{c.width}, baseline row {c.baseline}, "
                          f"gap {c.gap}). Extracted from {os.path.basename(args.font)}.")

    print(f"{args.font} -> {out}: {len(glyphs)} glyphs, grid {grid} units/dot")
    if clipped_names:
        print(f"  NOTE: ink outside the {c.height}x{c.width} cell was clipped on: {', '.join(clipped_names)}")


if __name__ == "__main__":
    main()
