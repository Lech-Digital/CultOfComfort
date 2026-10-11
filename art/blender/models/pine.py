"""A Marchland pine: three stacked cones on a stubby trunk, leaning a little (medieval things are wonky)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    tree = root("pine")
    (Part("Prop", parent=tree)
     .cylinder(0.08, 0.5, (0, 0, 0.25), "old_wood", segments=7)
     .cylinder(0.6, 0.42, (0, 0, 0.55), "pine", segments=10, radius_top=0.2, rotation=(2, -2, 0))            # tiers overhang so the
     .cylinder(0.46, 0.4, (0.01, 0, 0.9), "grass_alt", segments=10, radius_top=0.14, rotation=(-2, 3, 18))   # high camera sees rings
     .cylinder(0.32, 0.46, (0.03, -0.01, 1.24), "foliage", segments=9, radius_top=0.0, rotation=(3, 2, 36))
     .build())
    return tree
