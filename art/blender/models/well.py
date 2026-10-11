"""A village well: round stone wall, a little shingled roof on two posts, crank and bucket."""
from lib.buildings import thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    well = root("well")
    b = Part("Building", parent=well)
    b.cylinder(0.32, 0.4, (0, 0, 0.2), "fieldstone", segments=12)
    b.cylinder(0.34, 0.05, (0, 0, 0.42), "worn_stone", segments=12)
    b.cylinder(0.26, 0.02, (0, 0, 0.3), "water", segments=12)
    for x in (-0.3, 0.3):
        b.box((0.06, 0.06, 0.8), (x, 0, 0.8), "old_wood", bevel=0.01)
    thatch_roof(b, 0.55, 0.5, (0, 0, 1.15), 0.3, overhang=0.08, lean=0)
    b.cylinder(0.04, 0.62, (0, 0, 0.95), "tarred_wood", segments=6, rotation=(0, 90, 0))
    b.box((0.03, 0.12, 0.03), (0.34, -0.05, 0.95), "old_wood", bevel=0.004)                             # crank
    b.cylinder(0.065, 0.1, (0, -0.05, 0.6), "old_wood", segments=8, radius_top=0.075)
    b.build()
    return well
