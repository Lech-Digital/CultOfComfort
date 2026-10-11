"""World map: a half-unrolled chart with a red route."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_map")
    part = Part("Prop", parent=icon)
    part.box((0.42, 0.3, 0.01), (0, 0, 0.01), "daub", bevel=0)
    part.cylinder(0.04, 0.32, (0.22, 0, 0.04), "daub", segments=10, rotation=(90, 0, 0))
    part.box((0.22, 0.015, 0.012), (-0.05, 0.02, 0.02), "zealot_red", bevel=0, rotation=(0, 0, 30))
    part.box((0.05, 0.012, 0.012), (-0.15, -0.06, 0.02), "zealot_red", bevel=0, rotation=(0, 0, 45))
    part.box((0.05, 0.012, 0.012), (-0.15, -0.06, 0.02), "zealot_red", bevel=0, rotation=(0, 0, -45))
    part.build()
    return icon
