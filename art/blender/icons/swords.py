"""Fighting the Prophet: two crossed blades."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_swords")
    part = Part("Prop", parent=icon)
    for side in (-1, 1):
        part.box((0.04, 0.02, 0.44), (0, 0, 0.26), "mail", bevel=0, rotation=(0, side * 35, 0))
        part.box((0.14, 0.03, 0.03), (side * -0.1, -0.01, 0.1), "old_wood", bevel=0, rotation=(0, side * 35, 0))
        part.cylinder(0.02, 0.1, (side * -0.14, -0.01, 0.05), "tarred_wood", segments=6, rotation=(0, side * 35, 0))
    part.build()
    return icon
