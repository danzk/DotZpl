#!/usr/bin/env python3
"""Compile a .pixfont text source into a matrix-accurate ZPL pixel .ttf.

Each '#' pixel becomes a 1-dot square; squares are merged with skia-pathops (if present)
into clean rectangles. Metrics come from the font matrix (matrices.py): em = cell height,
monospace advance = width + intercharacter gap, vertical metrics from the baseline. The
result renders at exactly the matrix when drawn at emSize = matrixHeight.

Usage:
    python build_font.py glyphs/font-a.pixfont A -o ../../WpfZpl/Resources/font-a.ttf
"""
import argparse
import os
import sys

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

import pixfont
from matrices import cell

UNITS_PER_DOT = 64   # font units per pixel dot (em = height * UNITS_PER_DOT)


def build_glyph(rows, baseline, g=UNITS_PER_DOT):
    pen = TTGlyphPen(None)
    for r, row in enumerate(rows):
        y_top = (baseline - r) * g
        y_bot = y_top - g
        for c, ch in enumerate(row):
            if ch == pixfont.ON:
                x0, x1 = c * g, (c + 1) * g
                pen.moveTo((x0, y_bot))      # one square per dot (merged later)
                pen.lineTo((x1, y_bot))
                pen.lineTo((x1, y_top))
                pen.lineTo((x0, y_top))
                pen.closePath()
    return pen.glyph()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("source", help="input .pixfont")
    ap.add_argument("key", help="Zebra font key (A B C D E F G H GS)")
    ap.add_argument("-o", "--out", required=True, help="output .ttf")
    ap.add_argument("--dpmm", type=int, default=8, choices=(6, 8))
    args = ap.parse_args()

    c = cell(args.key, args.dpmm)
    _font_key, glyphs = pixfont.read(args.source)
    g = UNITS_PER_DOT
    upm = c.height * g
    ascent = c.baseline * g
    descent = -(c.height - c.baseline) * g
    advance = (c.width + (c.gap or 0)) * g

    order = [".notdef"] + [gl.name for gl in glyphs]
    fb = FontBuilder(upm, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap({gl.codepoint: gl.name for gl in glyphs})

    pen = TTGlyphPen(None)
    tt_glyphs = {".notdef": pen.glyph()}
    metrics = {".notdef": (advance, 0)}
    for gl in glyphs:
        tt_glyphs[gl.name] = build_glyph(gl.rows, c.baseline)
        metrics[gl.name] = (advance, 0)
    fb.setupGlyf(tt_glyphs)

    try:
        from fontTools.ttLib.removeOverlaps import removeOverlaps
        removeOverlaps(fb.font)                      # merge per-dot squares into rectangles
        merged = "merged"
    except ImportError:
        merged = "per-dot squares (install skia-pathops to merge)"

    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=ascent, descent=descent)
    fb.setupNameTable({"familyName": f"ZPL Font {args.key}", "styleName": "Regular"})
    fb.setupOS2(version=4, sTypoAscender=ascent, sTypoDescender=descent, sTypoLineGap=0,
                usWinAscent=ascent, usWinDescent=-descent, fsSelection=0x40 | 0x80)   # REGULAR | USE_TYPO_METRICS
    fb.setupPost()
    fb.font["post"].isFixedPitch = 1

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    fb.save(args.out)
    print(f"{args.source} -> {args.out}: {len(glyphs)} glyphs, UPM={upm}, "
          f"cell={c.height}x{c.width}, advance={c.width + (c.gap or 0)} dots, {merged}")


if __name__ == "__main__":
    main()
