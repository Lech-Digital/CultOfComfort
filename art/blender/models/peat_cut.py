"""Peat Cut: a dug trench in the bog, stacked drying bricks of peat and a spade stuck in the mud.
Peat heats the baths (economy: Heating upkeep)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    cut = root("peat_cut")
    b = Part("Building", parent=cut)
    b.box((1.3, 1.0, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    b.box((0.7, 0.4, 0.04), (-0.2, -0.15, 0.065), "wet_mud", bevel=0.02, bevel_segments=2)             # the trench
    b.box((0.66, 0.36, 0.02), (-0.2, -0.15, 0.08), "water", bevel=0.01)                                 # bog water
    for stack, (sx, sy) in enumerate(((0.3, 0.05), (0.3, 0.3), (0.05, 0.32))):                  # neat drying stacks
        for row in range(3):
            for col in range(2 - row // 2):
                b.box((0.18, 0.1, 0.08), (sx + col * 0.05 * (row % 2), sy + (col - 0.5) * 0.11, 0.1 + row * 0.085),
                      "tarred_wood" if (row + col + stack) % 2 else "wet_mud", bevel=0.01, rotation=(0, 0, row * 90 + stack * 10))
    b.cylinder(0.015, 0.5, (-0.45, 0.2, 0.3), "fresh_log", segments=5, rotation=(12, -8, 0))           # spade
    b.box((0.12, 0.02, 0.15), (-0.43, 0.18, 0.08), "mail", bevel=0.004, rotation=(12, -8, 0))
    b.box((0.06, 0.6, 0.06), (0.4, -0.32, 0.4), "old_wood", bevel=0.008)                               # drying rack
    for y in (-0.6, -0.05):
        b.box((0.05, 0.05, 0.42), (0.4, y, 0.21), "old_wood", bevel=0.006)
    b.build()
    return cut
