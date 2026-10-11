"""A big round oak: clumpy, friendly canopy (smooth), knobbly trunk with a branch stub."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    tree = root("oak")
    (Part("Prop", parent=tree)
     .cylinder(0.13, 0.8, (0, 0, 0.4), "old_wood", segments=8, radius_top=0.09, rotation=(0, 3, 0))
     .cylinder(0.045, 0.35, (0.14, 0, 0.75), "old_wood", segments=6, rotation=(0, 50, 0))
     .sphere(0.45, (0, 0, 1.15), "foliage", segments=9, rings=6, smooth=True)
     .sphere(0.33, (0.35, 0.1, 1.0), "foliage", segments=8, rings=6, smooth=True)
     .sphere(0.32, (-0.3, -0.15, 1.05), "grass", segments=8, rings=6, smooth=True)
     .sphere(0.28, (0.05, 0.3, 1.4), "foliage", segments=8, rings=6, smooth=True)
     .build())
    return tree
