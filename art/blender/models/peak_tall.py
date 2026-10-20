"""A tall lone peak: a sharp grey spire with a big snow cap and a lesser peak at its shoulder."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    peak = root("peak_tall")
    (Part("Prop", parent=peak)
     .cylinder(1.8, 5.4, (0, 0, 2.7), "fieldstone", segments=7, radius_top=0.08, rotation=(0, 0, 8))
     .cylinder(0.75, 2.0, (0.02, 0.0, 4.42), "linen", segments=7, radius_top=0.02, rotation=(0, 0, 8))                   # snow
     .cylinder(1.2, 3.1, (1.4, 0.6, 1.55), "worn_stone", segments=6, radius_top=0.1, rotation=(4, -3, 30))                # shoulder
     .cylinder(0.4, 0.9, (1.43, 0.61, 2.7), "linen", segments=6, radius_top=0.02, rotation=(4, -3, 30))
     .sphere(0.7, (-1.1, -1.0, 0.2), "worn_stone", segments=6, rings=4, scale=(1.4, 1.0, 0.5))                            # scree
     .build())
    return peak
