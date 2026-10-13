"""Peat Cut: a dug trench in the bog, stacked drying bricks of peat and a spade stuck in the mud.
Peat heats the baths (economy: Heating upkeep). Laid out long, like its 2×4 plot."""
from lib.buildings import peat_stack
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    cut = root("peat_cut")
    b = Part("Building", parent=cut)
    b.box((1.0, 1.7, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    b.box((0.42, 1.0, 0.04), (-0.2, -0.1, 0.065), "wet_mud", bevel=0.02, bevel_segments=2)             # the trench
    b.box((0.38, 0.96, 0.02), (-0.2, -0.1, 0.08), "water", bevel=0.01)                                  # bog water
    for k, y in enumerate((-0.5, -0.1, 0.3)):                                                            # neat drying stacks
        peat_stack(b, (0.25, y, 0.06), yaw=k * 10)
    b.cylinder(0.015, 0.5, (-0.38, 0.55, 0.3), "fresh_log", segments=5, rotation=(12, -8, 0))           # spade
    b.box((0.12, 0.02, 0.15), (-0.36, 0.53, 0.08), "mail", bevel=0.004, rotation=(12, -8, 0))
    b.box((0.06, 0.55, 0.06), (0.35, 0.55, 0.4), "old_wood", bevel=0.008)                               # drying rack
    for y in (0.3, 0.8):
        b.box((0.05, 0.05, 0.42), (0.35, y, 0.21), "old_wood", bevel=0.006)
    b.build()
    return cut
