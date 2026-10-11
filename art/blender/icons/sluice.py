"""Open Sluice: a plank gate raised between two posts over water."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_sluice")
    part = Part("Prop", parent=icon)
    part.box((0.5, 0.2, 0.06), (0, 0, 0.03), "water", bevel=0)
    for x in (-0.2, 0.2):
        part.box((0.06, 0.06, 0.5), (x, 0, 0.25), "old_wood", bevel=0.01)
    part.box((0.36, 0.04, 0.2), (0, 0, 0.32), "fresh_log", bevel=0.01)
    part.box((0.5, 0.07, 0.05), (0, 0, 0.52), "old_wood", bevel=0.01)
    part.build()
    return icon
