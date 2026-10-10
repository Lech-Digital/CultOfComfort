"""Woodcutter's Lean-to (tech tree: 'a man, an axe, low expectations'): a sloping board roof on posts,
a log pile, a chopping stump with the axe in it and a saw-horse."""
from lib.buildings import plank_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    lean_to = root("woodcutter")
    b = Part("Building", parent=lean_to)
    b.box((1.3, 1.0, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)                                     # trodden floor
    for x, y, h in ((-0.55, -0.4, 0.75), (0.55, -0.4, 0.72), (-0.55, 0.4, 1.05), (0.55, 0.4, 1.08)):
        b.box((0.08, 0.08, h), (x, y, h / 2), "old_wood", bevel=0.012, rotation=((x > 0) * 2 - 1, 0, 0))
    plank_roof(b, 1.3, 1.05, (0, 0, 0.92), 16)
    for k in range(3):                                                                                  # log pile
        for j in range(3 - k):
            b.cylinder(0.07, 0.7, (-0.05 + (j - (2 - k) / 2) * 0.15, 0.28, 0.1 + k * 0.12), "fresh_log", segments=7,
                       rotation=(0, 90, 0))
    b.cylinder(0.14, 0.24, (0.35, -0.25, 0.12), "old_wood", segments=8)                                 # chopping stump
    b.cylinder(0.12, 0.02, (0.35, -0.25, 0.245), "fresh_log", segments=8)
    b.cylinder(0.015, 0.34, (0.42, -0.28, 0.38), "fresh_log", segments=5, rotation=(0, 35, 0))          # axe
    b.box((0.12, 0.02, 0.08), (0.35, -0.28, 0.27), "mail", bevel=0.004, rotation=(0, 35, 0))
    b.box((0.06, 0.5, 0.06), (-0.35, -0.3, 0.3), "old_wood", bevel=0.008)                               # saw-horse
    for y in (-0.5, -0.1):
        b.box((0.3, 0.05, 0.05), (-0.35, y, 0.15), "old_wood", bevel=0.006, rotation=(0, 60, 0))
    b.build()
    return lean_to
