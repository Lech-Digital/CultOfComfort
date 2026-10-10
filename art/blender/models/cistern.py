"""Cistern: a round stone tank with a wooden lid, a bucket on a rope from an A-frame, and a gutter feeding it.
It refills the Sluice Gate (combat: Open Sluice)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    cistern = root("cistern")
    b = Part("Building", parent=cistern)
    b.cylinder(0.45, 0.55, (0, 0, 0.275), "fieldstone", segments=14)
    b.cylinder(0.48, 0.06, (0, 0, 0.56), "worn_stone", segments=14)
    b.cylinder(0.4, 0.02, (0, 0, 0.58), "water", segments=14)
    b.box((0.45, 0.5, 0.04), (0.18, 0.05, 0.6), "old_wood", bevel=0.01, rotation=(0, 4, 8))           # half a lid
    for x in (-0.42, 0.42):                                                                            # A-frame
        b.box((0.06, 0.06, 1.05), (x, 0, 0.6), "old_wood", bevel=0.01, rotation=(0, -12 if x > 0 else 12, 0))
    b.cylinder(0.04, 0.95, (0, 0, 1.05), "tarred_wood", segments=6, rotation=(0, 90, 0))
    b.cylinder(0.006, 0.35, (0, 0, 0.88), "thatch", segments=4)
    b.cylinder(0.07, 0.1, (0, 0, 0.66), "old_wood", segments=8, radius_top=0.08)                      # bucket
    b.box((0.1, 0.9, 0.06), (-0.55, 0.35, 0.75), "old_wood", bevel=0.01, rotation=(14, 0, 30))         # gutter
    b.build()
    return cistern
