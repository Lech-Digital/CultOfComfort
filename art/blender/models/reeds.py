"""Reeds on a river bank: a clump of thin green stalks, a few with brown cattail heads."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400
REQUIRED_PARTS = ("Prop",)

STALKS = ((0, 0, 0.55, 0), (0.07, 0.05, 0.48, 9), (-0.06, 0.04, 0.42, -8), (0.03, -0.07, 0.5, 6), (-0.05, -0.05, 0.38, -12),
          (0.11, -0.02, 0.34, 14), (-0.1, 0.0, 0.46, -4))


def build():
    reeds = root("reeds")
    p = Part("Prop", parent=reeds)
    for k, (x, y, h, lean) in enumerate(STALKS):
        p.cylinder(0.012, h, (x, y, h / 2), "foliage", segments=3, rotation=(lean, lean / 2, 0))
        if k % 3 == 0:
            p.cylinder(0.025, 0.09, (x + lean * 0.004, y, h + 0.02), "tarred_wood", segments=4, rotation=(lean, lean / 2, 0))   # cattail
    p.build()
    return reeds
