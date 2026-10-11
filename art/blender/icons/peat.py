"""Peat: a cut brick of bog."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_peat")
    part = Part("Prop", parent=icon)
    part.box((0.42, 0.26, 0.2), (0, 0, 0.1), "wet_mud", bevel=0.03, rotation=(0, 0, 8))
    part.box((0.36, 0.22, 0.04), (0.02, 0, 0.215), "tarred_wood", bevel=0.01, rotation=(0, 3, 8))
    part.build()
    return icon
