"""Deep forest: a dark clump of tall old pines, packed too tight to build in. The village's woods are lighter."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 700
REQUIRED_PARTS = ("Prop",)

_TREES = ((0.0, 0.0, 1.0), (0.55, 0.35, 0.85), (-0.5, 0.4, 0.9), (0.2, -0.55, 0.95), (-0.45, -0.45, 0.8))


def build():
    forest = root("deep_forest")
    part = Part("Prop", parent=forest)
    for x, y, s in _TREES:
        (part.cylinder(0.07 * s, 0.4 * s, (x, y, 0.2 * s), "tarred_wood", segments=5)
             .cylinder(0.42 * s, 0.9 * s, (x, y, 0.75 * s), "pine", segments=7, radius_top=0.12 * s)
             .cylinder(0.3 * s, 0.8 * s, (x, y, 1.35 * s), "pine", segments=7, radius_top=0.0))
    part.build()
    return forest
