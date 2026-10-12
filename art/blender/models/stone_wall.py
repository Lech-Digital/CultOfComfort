"""A stone wall segment, one tile long: two staggered courses of rough-cut blocks and a capstone row. Wulfric's men hate it."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    wall = root("stone_wall")
    p = Part("Prop", parent=wall)
    p.box((0.98, 0.26, 0.6), (0, 0, 0.3), "fieldstone", bevel=0.01)                     # the core, no gaps
    for k, x in enumerate((-0.37, -0.123, 0.123, 0.37)):                                 # bottom course
        p.box((0.24, 0.3, 0.29), (x, 0, 0.15), "worn_stone" if k % 2 else "fieldstone", bevel=0.025)
    for k, (x, w) in enumerate(((-0.43, 0.12), (-0.25, 0.24), (0.0, 0.24), (0.25, 0.24), (0.43, 0.12))):  # top course, staggered
        p.box((w, 0.3, 0.29), (x, 0, 0.45), "fieldstone" if k % 2 else "worn_stone", bevel=0.025)
    p.box((1.0, 0.32, 0.08), (0, 0, 0.64), "worn_stone", bevel=0.02)                    # capstones
    p.build()
    return wall
