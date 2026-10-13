"""Bathhouse: hot water and a bench. Revolutionary. A timber bath-hall on an old Roman stone base, a steaming
open-air pool out front with a carved duck spouting water into it, wooden tubs, towel lines in every dye, and a
peat stove chimney that never stops. The first building big enough to have a front garden."""
from lib.buildings import flip_flops, rubber_duck, thatch_roof, towel_line
from lib.mesh import Part, root

TRIANGLE_BUDGET = 6000
REQUIRED_PARTS = ("Building",)


def build():
    bath = root("bathhouse")
    b = Part("Building", parent=bath)
    b.box((1.06, 1.06, 0.06), (0, 0, 0.03), "worn_stone", bevel=0.02)                                        # Roman base
    b.box((1.0, 0.04, 0.04), (0, -0.51, 0.08), "roman_marble", bevel=0.01)
    # The hall at the back.
    b.box((0.9, 0.42, 0.42), (0, 0.24, 0.27), "daub", bevel=0.03)
    for x in (-0.45, -0.15, 0.15, 0.45):
        b.box((0.05, 0.44, 0.44), (x, 0.24, 0.27), "old_wood", bevel=0.008)
    thatch_roof(b, 0.9, 0.42, (0, 0.24, 0.48), 0.32, overhang=0.05)
    for x in (-0.3, 0.3):
        b.box((0.12, 0.03, 0.1), (x, 0.02, 0.34), "window", bevel=0.005)
    b.box((0.16, 0.04, 0.26), (0, 0.02, 0.19), "tarred_wood", bevel=0.01)                                    # door
    b.box((0.12, 0.12, 0.42), (0.36, 0.34, 0.75), "fieldstone", bevel=0.015)                                 # the peat stove's chimney
    for k, (r, z) in enumerate(((0.06, 1.0), (0.05, 1.1), (0.04, 1.19))):
        b.sphere(r, (0.37 + k * 0.02, 0.34, z), "linen", segments=6, rings=4)
    # The pool out front, steaming, with the duck fountain.
    b.box((0.62, 0.36, 0.06), (-0.12, -0.24, 0.09), "roman_marble", bevel=0.015)
    b.box((0.54, 0.28, 0.02), (-0.12, -0.24, 0.12), "water", bevel=0.005)
    b.box((0.08, 0.08, 0.16), (-0.36, -0.24, 0.2), "worn_stone", bevel=0.01)
    rubber_duck(b, (-0.36, -0.24, 0.28), size=0.6)
    b.cylinder(0.008, 0.16, (-0.29, -0.24, 0.27), "water", segments=4, rotation=(0, 60, 0))                  # the spout
    for k in range(4):
        b.sphere(0.05 - k * 0.006, (-0.05 + k * 0.06, -0.24, 0.2 + k * 0.05), "linen", segments=6, rings=4)
    # Tubs and the towel line.
    for x in (0.33, 0.45):
        b.cylinder(0.06, 0.08, (x, -0.32, 0.1), "old_wood", segments=8)
        b.cylinder(0.05, 0.01, (x, -0.32, 0.145), "water", segments=8)
    towel_line(b, (0.25, -0.12), (0.5, -0.12), 0.3, ("woad", "madder", "weld"))
    flip_flops(b, (0.05, -0.04, 0.06))
    b.build()
    return bath
