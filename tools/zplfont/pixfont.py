"""Read/write the .pixfont text source format for ZPL pixel fonts.

A .pixfont file is the editable source of truth for a font: a list of glyphs, each a
grid of '#' (ink) / '.' (empty) rows. The grid is the matrix cell (H rows x W cols),
with row 0 at the top of the cell. Comments start with ';'.

    ; ZPL Font A  (cell 9x5, baseline row 7)
    font: A

    U+0041 A
    .###.
    #...#
    #...#
    #####
    #...#
    #...#
    #...#
    .....
    .....
"""
import re
from dataclasses import dataclass, field


ON, OFF = "#", "."


@dataclass
class PixGlyph:
    codepoint: int
    name: str
    rows: list = field(default_factory=list)   # list[str] of ON/OFF chars


def read(path):
    """Return (font_key, [PixGlyph...]). font_key is the 'font:' header (or None)."""
    font_key = None
    glyphs = []
    current = None
    with open(path, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\n")
            stripped = line.strip()
            if not stripped or stripped.startswith(";"):
                continue
            header = re.match(r"^font:\s*(\S+)$", stripped)
            if header:
                font_key = header.group(1)
                continue
            glyph = re.match(r"^U\+([0-9A-Fa-f]+)\s+(\S+)\s*$", stripped)
            if glyph:
                current = PixGlyph(int(glyph.group(1), 16), glyph.group(2))
                glyphs.append(current)
                continue
            if current is not None and stripped and set(stripped) <= {ON, OFF}:
                current.rows.append(stripped)
    return font_key, glyphs


def write(path, font_key, glyphs, comment=None):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        if comment:
            for line in comment.splitlines():
                fh.write(f"; {line}\n")
        fh.write(f"font: {font_key}\n\n")
        for g in glyphs:
            fh.write(f"U+{g.codepoint:04X} {g.name}\n")
            for row in g.rows:
                fh.write(row + "\n")
            fh.write("\n")
