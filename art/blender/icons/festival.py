"""The Comfort Festival: a string of bunting between two poles, flags in woad, madder and yellow."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400


def build():
    icon = root("icon_festival")
    part = Part("Prop", parent=icon)
    for x in (-0.22, 0.22):
        part.cylinder(0.014, 0.34, (x, 0, 0.17), "old_wood", segments=5)
    part.cylinder(0.005, 0.44, (0, 0, 0.3), "thatch", segments=4, rotation=(0, 90, 0))
    for k, colour in enumerate(("woad", "madder", "weld", "woad", "madder")):
        x = -0.16 + k * 0.08
        part.box((0.05, 0.01, 0.08), (x, 0, 0.255 - abs(k - 2) * 0.006), colour, bevel=0, rotation=(0, 45, 0))
    part.build()
    return icon
