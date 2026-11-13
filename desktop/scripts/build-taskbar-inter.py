"""Freeze the approved AsterHub taskbar preview's Inter at its two used weights.

Usage: python scripts/build-taskbar-inter.py <preview/fonts/inter.woff2>
The GDI digit compositor uses a tabular cmap so shaping a single glyph and
shaping a CSS tabular number use the same outline and advance.
"""
from pathlib import Path
import sys

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

source = Path(sys.argv[1])
output = Path(__file__).resolve().parents[1] / "host" / "fonts"
for weight, style in ((400, "Regular"), (620, "Readout")):
    font = instantiateVariableFont(TTFont(source), {"wght": weight})
    if weight == 620:
        gsub = font["GSUB"].table
        tabular = next(r.Feature for r in gsub.FeatureList.FeatureRecord if r.FeatureTag == "tnum")
        mapping = {}
        for lookup in tabular.LookupListIndex:
            for subtable in gsub.LookupList.Lookup[lookup].SubTable:
                mapping.update(subtable.mapping)
        for table in font["cmap"].tables:
            if table.isUnicode():
                for char in "0123456789.":
                    key = ord(char)
                    table.cmap[key] = mapping.get(table.cmap[key], table.cmap[key])
    family = f"AsterHub Taskbar {style}"
    names = {1: family, 2: "Regular", 3: family, 4: family, 6: family.replace(" ", ""),
             16: family, 17: "Regular", 21: family, 22: "Regular"}
    for record in font["name"].names:
        if record.nameID in names:
            record.string = names[record.nameID]
    font.flavor = None
    font.save(output / f"TaskbarInter0423-{style}.ttf")