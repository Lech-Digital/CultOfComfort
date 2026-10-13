"""Peat Cut, level 2 — the Terraced Cut: two trenches and a plank walk between them, a wheelbarrow of bricks,
and a drying shed roofed with a blue tarp, because the bog gets rained on even more than the bog."""
from lib.buildings import peat_stack
from lib.mesh import Part, root

TRIANGLE_BUDGET = 4000
REQUIRED_PARTS = ("Building",)


def build():
    cut = root("peat_cut_l2")
    b = Part("Building", parent=cut)
    b.box((1.04, 1.98, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    for y in (-0.55, 0.15):                                                                              # two trenches
        b.box((0.44, 0.6, 0.04), (-0.22, y, 0.065), "wet_mud", bevel=0.02, bevel_segments=2)
        b.box((0.4, 0.56, 0.02), (-0.22, y, 0.08), "water", bevel=0.01)
    for k in range(4):                                                                                   # plank walk
        b.box((0.5, 0.06, 0.025), (-0.22, -0.28 + (k - 1.5) * 0.065, 0.085), "fresh_log" if k % 2 else "old_wood", bevel=0.004,
              rotation=(0, 0, (k % 2) * 4 - 2))
    # Drying shed: four posts and a tarp, stacks underneath.
    for x in (0.1, 0.46):
        for y in (0.25, 0.9):
            b.box((0.05, 0.05, 0.72), (x, y, 0.36), "old_wood", bevel=0.006)
    b.box((0.5, 0.78, 0.02), (0.28, 0.57, 0.74), "tarp_blue", bevel=0, rotation=(0, -10, 2))
    for y in (0.4, 0.75):
        peat_stack(b, (0.28, y, 0.06), rows=4)
    peat_stack(b, (0.3, -0.05, 0.06), rows=3, yaw=15)
    # Wheelbarrow full of peat.
    b.box((0.26, 0.36, 0.12), (0.25, -0.62, 0.2), "old_wood", bevel=0.015, rotation=(-6, 0, 0))
    b.cylinder(0.07, 0.04, (0.25, -0.84, 0.09), "tarred_wood", segments=10, rotation=(0, 90, 0))
    for x in (0.16, 0.34):
        b.box((0.03, 0.4, 0.03), (x, -0.38, 0.22), "fresh_log", bevel=0.004, rotation=(-12, 0, 0))
    for k in range(3):
        b.box((0.16, 0.09, 0.07), (0.25 + (k - 1) * 0.06, -0.62 + (k % 2) * 0.08, 0.3), "tarred_wood", bevel=0.008, rotation=(0, 0, k * 35))
    b.cylinder(0.015, 0.5, (-0.42, 0.65, 0.3), "fresh_log", segments=5, rotation=(12, -8, 0))           # spade
    b.box((0.12, 0.02, 0.15), (-0.4, 0.63, 0.08), "mail", bevel=0.004, rotation=(12, -8, 0))
    b.build()
    return cut
