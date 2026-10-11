"""Scrap: a bent horseshoe and a nail."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_scrap")
    part = Part("Prop", parent=icon)
    for k in range(9):
        a = math.radians(-20 + k * 220 / 8)
        part.box((0.08, 0.06, 0.07), (math.cos(a) * 0.2, 0, 0.22 + math.sin(a) * 0.2), "mail", bevel=0,
                 rotation=(0, -math.degrees(a) + 90, 0))
    part.cylinder(0.012, 0.3, (0.05, -0.06, 0.12), "fieldstone", segments=5, rotation=(0, 70, 0))
    part.build()
    return icon
