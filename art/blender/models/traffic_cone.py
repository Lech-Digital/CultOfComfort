"""A traffic cone from 2024. Perfectly geometric, aggressively orange, the only thing in 947 that is exactly right."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    cone = root("traffic_cone")
    (Part("Prop", parent=cone)
     .box((0.3, 0.3, 0.03), (0, 0, 0.015), "cone_orange", bevel=0.006)                       # square base
     .cylinder(0.11, 0.42, (0, 0, 0.24), "cone_orange", segments=12, radius_top=0.025)
     .cylinder(0.085, 0.06, (0, 0, 0.2), "linen", segments=12, radius_top=0.072)             # reflective stripe
     .build())
    return cone
