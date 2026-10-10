"""A dead tree: tarred-black trunk and crooked branches. Atmospheric; mostly sulking."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    tree = root("dead_tree")
    (Part("Prop", parent=tree)
     .cylinder(0.11, 1.0, (0, 0, 0.5), "tarred_wood", segments=7, radius_top=0.06, rotation=(4, -6, 0))
     .cylinder(0.04, 0.55, (0.17, 0.02, 0.95), "tarred_wood", segments=5, radius_top=0.01, rotation=(0, 55, 10))
     .cylinder(0.035, 0.45, (-0.14, -0.05, 1.05), "tarred_wood", segments=5, radius_top=0.01, rotation=(10, -45, 0))
     .cylinder(0.03, 0.35, (0.0, 0.12, 1.15), "old_wood", segments=5, radius_top=0.008, rotation=(-40, 5, 0))
     .cylinder(0.025, 0.25, (0.33, 0.04, 1.2), "tarred_wood", segments=5, radius_top=0.006, rotation=(0, 15, 0))
     .build())
    return tree
