"""A standing stone, older than the lord and the bishop both: tall, leaning a little, a crust of moss on one side.
In a ring they make a circle the cult would very much like to hold a service in."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    stone = root("standing_stone")
    (Part("Prop", parent=stone)
     .box((0.32, 0.2, 1.1), (0, 0, 0.55), "worn_stone", bevel=0.05, bevel_segments=2, rotation=(3, -4, 0))
     .box((0.06, 0.18, 0.4), (-0.15, 0.0, 0.35), "foliage", bevel=0.02, rotation=(3, -4, 0))                            # moss
     .build())
    return stone
