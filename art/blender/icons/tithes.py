"""Tithes: a coin pouch, tied, with coins spilling out."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_tithes")
    part = Part("Prop", parent=icon)
    part.sphere(0.15, (0, 0, 0.14), "levy_wool", segments=10, rings=7, scale=(1.0, 1.0, 0.9), smooth=True)
    part.cylinder(0.05, 0.08, (0, 0, 0.3), "levy_wool", segments=8, smooth=True)
    part.cylinder(0.056, 0.02, (0, 0, 0.28), "tarred_wood", segments=8)
    for x, y in ((0.18, -0.08), (0.24, 0.02), (0.13, -0.16)):
        part.cylinder(0.04, 0.012, (x, y, 0.006), "gold_trim", segments=10)
    part.build()
    return icon
