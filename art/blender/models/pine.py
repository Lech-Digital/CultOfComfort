"""A Marchland pine: three stacked cones on a stubby trunk, leaning a little (medieval things are wonky)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    tree = root("pine")
    # The remodel (the target frame day-final): taller, four drooping tiers in one deep pine green, the tips a shade
    # lighter, so a forest reads as pines and not as a stack of colours.
    p = Part("Prop", parent=tree)
    p.cylinder(0.08, 0.45, (0, 0, 0.22), "old_wood", segments=7)
    for k, (r, z, h) in enumerate(((0.62, 0.55, 0.5), (0.48, 0.95, 0.46), (0.35, 1.3, 0.42), (0.22, 1.6, 0.42))):
        p.cylinder(r, h, (0.01 * k, -0.005 * k, z), "pine", segments=10, radius_top=r * 0.08 if k < 3 else 0.0,
                   rotation=((k % 2) * 3 - 1.5, 2 - k, k * 17))
    p.cylinder(0.05, 0.14, (0.04, -0.02, 1.86), "grass_alt", segments=6, radius_top=0.0)
    p.build()
    return tree
