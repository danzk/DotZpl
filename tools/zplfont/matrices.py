"""Zebra ZPL font matrices, in dots.

Combines the printer's "Font Matrices" tables (cell H x W) with Table 29
"Intercharacter Gap and Baseline Parameters":

  * height, width  -- character cell, in dots (H x W)
  * gap            -- intercharacter gap in dots (advance pitch = width + gap);
                      None means proportional (no fixed monospace advance)
  * baseline       -- dots from the top of the cell to the baseline
                      (= dots above the baseline = ascent; descent = height - baseline)

Type key: U = uppercase, L = lowercase, D = descenders.
Most fonts are density-independent; E and H differ between 6 and 8 dot/mm.
"""
from collections import namedtuple

Cell = namedtuple("Cell", "height width gap baseline")

# 8 dot/mm (203 dpi) printhead.
MATRIX_8DPMM = {
    "A": Cell(9, 5, 1, 7),
    "B": Cell(11, 7, 2, 11),
    "C": Cell(18, 10, 2, 14),
    "D": Cell(18, 10, 2, 14),
    "E": Cell(28, 15, 5, 23),
    "F": Cell(26, 13, 3, 21),
    "G": Cell(60, 40, 8, 48),
    "H": Cell(21, 13, 6, 21),
    "GS": Cell(24, 24, None, 18),  # symbol; proportional gap; baseline = 3*H/4
}

# 6 dot/mm printhead: only E and H change cell size (Table 30).
MATRIX_6DPMM = dict(MATRIX_8DPMM, E=Cell(21, 10, 5, 23), H=Cell(17, 11, 6, 21))


def cell(font_key: str, dpmm: int = 8) -> Cell:
    table = MATRIX_6DPMM if dpmm == 6 else MATRIX_8DPMM
    if font_key not in table:
        raise KeyError(f"unknown font '{font_key}'; known: {', '.join(table)}")
    return table[font_key]
