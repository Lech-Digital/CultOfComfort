"""A palisade segment of sharpened stakes, one tile long, slightly uneven (they were drunk when they built it)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    palisade = root("palisade")
    p = Part("Prop", parent=palisade)
    for k in range(7):
        x = -0.45 + k * 0.15
        h = 0.95 + (k * 37 % 5) * 0.04
        p.cylinder(0.065, h, (x, 0, h / 2), "old_wood" if k % 2 else "fresh_log", segments=6, rotation=((k % 3 - 1) * 2, 0, 0))
        p.cylinder(0.065, 0.14, (x, 0, h + 0.07), "fresh_log", segments=6, radius_top=0.0)
    p.box((1.0, 0.04, 0.08), (0, -0.07, 0.55), "tarred_wood", bevel=0.008)
    p.build()
    return palisade
