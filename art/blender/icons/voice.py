"""Phantom Voice: a tin-can telephone."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_voice")
    part = Part("Prop", parent=icon)
    part.cylinder(0.07, 0.14, (-0.18, 0, 0.12), "mail", segments=10, rotation=(0, 90, 0))
    part.cylinder(0.07, 0.14, (0.18, 0, 0.3), "mail", segments=10, rotation=(0, 90, 0))
    part.cylinder(0.006, 0.32, (0, 0, 0.21), "linen", segments=4, rotation=(0, 61, 0))
    part.build()
    return icon
