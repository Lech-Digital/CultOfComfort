"""A Marchland mountain: faceted grey peaks with snow on top. Nobody builds here; nobody even tries."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 700
REQUIRED_PARTS = ("Prop",)


def build():
    mountain = root("mountain")
    (Part("Prop", parent=mountain)
     .cylinder(1.6, 2.6, (0, 0, 1.3), "fieldstone", segments=6, radius_top=0.12, rotation=(0, 0, 10))
     .cylinder(0.42, 0.75, (0.04, 0.02, 2.3), "linen", segments=6, radius_top=0.02, rotation=(0, 0, 10))               # snow cap
     .cylinder(1.1, 1.7, (1.05, -0.65, 0.85), "worn_stone", segments=5, radius_top=0.1, rotation=(3, -4, 40))         # shoulder
     .cylinder(0.3, 0.42, (1.08, -0.66, 1.5), "linen", segments=5, radius_top=0.02, rotation=(3, -4, 40))
     .cylinder(0.95, 1.3, (-1.0, 0.7, 0.65), "fieldstone", segments=5, radius_top=0.12, rotation=(-4, 2, 25))         # foothill
     .sphere(0.5, (0.5, 1.1, 0.15), "worn_stone", segments=6, rings=4, scale=(1.3, 1.0, 0.5))                          # scree
     .build())
    return mountain
