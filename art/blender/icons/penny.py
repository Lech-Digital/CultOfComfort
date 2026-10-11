"""Silver Penny: a coin, face to the viewer."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_penny")
    part = Part("Prop", parent=icon)
    part.cylinder(0.2, 0.04, (0, 0, 0.2), "mail", segments=16, rotation=(90, 0, 0))
    part.cylinder(0.15, 0.01, (0, -0.022, 0.2), "worn_stone", segments=16, rotation=(90, 0, 0))
    part.box((0.03, 0.01, 0.16), (0, -0.03, 0.2), "fieldstone", bevel=0)
    part.box((0.16, 0.01, 0.03), (0, -0.03, 0.2), "fieldstone", bevel=0)
    part.build()
    return icon
