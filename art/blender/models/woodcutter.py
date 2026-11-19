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
    b.box((0.06, 0.5, 0.06), (-0.35, -0.3, 0.3), "old_wood", bevel=0.008)                               # saw-horse
    for y in (-0.5, -0.1):
        b.box((0.3, 0.05, 0.05), (-0.35, y, 0.15), "old_wood", bevel=0.006, rotation=(0, 60, 0))
    # The remodel (the target frame day-final): stone pads under the posts, and the work out in the open where the
    # camera sees it from above — a woodpile against the side, the splitting stump with its axe in front, chips about.
    for x, y in ((-0.55, -0.4), (0.55, -0.4), (-0.55, 0.4), (0.55, 0.4)):
        b.box((0.17, 0.17, 0.07), (x, y, 0.06), "fieldstone", bevel=0.02, bevel_segments=2)
    b.box((0.5, 0.42, 0.04), (0.2, -0.72, 0.02), "road_mud", bevel=0.015)                               # the yard
    b.cylinder(0.17, 0.26, (0.25, -0.75, 0.13), "old_wood", segments=9)                                 # splitting stump
    b.cylinder(0.15, 0.02, (0.25, -0.75, 0.265), "fresh_log", segments=9)
    b.cylinder(0.022, 0.46, (0.33, -0.78, 0.48), "fresh_log", segments=6, rotation=(0, 32, 0))          # the big axe
    b.box((0.17, 0.03, 0.12), (0.22, -0.78, 0.3), "mail", bevel=0.006, rotation=(0, 32, 0))
    for k in range(3):                                                                                  # woodpile, outside
        for j in range(4 - k):
            b.cylinder(0.075, 0.42, (0.9, -0.25 + j * 0.15 + k * 0.075, 0.08 + k * 0.13), "fresh_log", segments=8,
                       rotation=(0, 90, 0))
    for i, (x, y) in enumerate(((0.05, -0.68), (0.42, -0.6), (0.1, -0.9), (0.38, -0.92))):              # chips
        b.box((0.07, 0.03, 0.02), (x, y, 0.05), "fresh_log", bevel=0.004, rotation=(0, 0, i * 47))
    b.build()
    return lean_to
