"""Cultist hut: wattle-and-daub walls, a thatched roof patched with woad cloth, and a washing line of
towels.
Cleanliness is next to Prophet-ness."""
from lib.buildings import thatch_roof, wattle_fence
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    hut = root("cultist_hut")
    b = Part("Building", parent=hut)
    b.box((1.3, 1.1, 0.06), (0, 0, 0.03), "grass", bevel=0.02)
    b.box((0.8, 0.7, 0.5), (-0.1, 0.05, 0.31), "daub", bevel=0.03, rotation=(0, 0, 2))                 # walls
    for x in (-0.5, 0.3):
        b.box((0.06, 0.72, 0.52), (x, 0.05, 0.31), "old_wood", bevel=0.01, rotation=(0, 0, 2))
    thatch_roof(b, 0.8, 0.7, (-0.1, 0.05, 0.56), 0.5)
    b.box((0.3, 0.35, 0.02), (0.02, -0.05, 0.85), "woad", bevel=0, rotation=(0, 32, 2))                # woad patch
    b.box((0.2, 0.04, 0.34), (-0.05, -0.31, 0.23), "tarred_wood", bevel=0.01)                          # door
    b.box((0.12, 0.03, 0.1), (-0.32, -0.31, 0.38), "window", bevel=0.005)
    # Washing line with towels.
    for x in (0.4, 0.62):
        b.cylinder(0.02, 0.7, (x, -0.45 + (x - 0.4) * 2.0, 0.35), "old_wood", segments=5)
    b.cylinder(0.005, 0.5, (0.51, -0.23, 0.68), "thatch", segments=4, rotation=(90, 0, -27))
    for k, colour in enumerate(("linen", "woad", "linen")):
        b.box((0.12, 0.015, 0.18), (0.44 + k * 0.07, -0.36 + k * 0.14, 0.58), colour, bevel=0.003, rotation=(0, 0, -27))
    wattle_fence(b, (-0.6, 0.5), (0.5, 0.5), height=0.3)
    b.build()
    return hut
