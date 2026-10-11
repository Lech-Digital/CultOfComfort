"""Missionaries: a neon sandal and a walking staff."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_missionaries")
    part = Part("Prop", parent=icon)
    part.box((0.14, 0.34, 0.03), (-0.06, 0, 0.015), "flipflop_pink", bevel=0.01)
    part.box((0.15, 0.03, 0.05), (-0.06, -0.06, 0.05), "tarred_wood", bevel=0.005)
    part.box((0.03, 0.12, 0.04), (-0.06, -0.12, 0.045), "tarred_wood", bevel=0.005)
    part.cylinder(0.018, 0.6, (0.12, 0.04, 0.3), "old_wood", segments=6, rotation=(0, 12, 0))
    part.build()
    return icon
