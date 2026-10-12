"""A dirt track tile: packed earth with a puddle and a few pebbles. The cheapest way to say 'this way to the Hearth'."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 200
REQUIRED_PARTS = ("Prop",)


def build():
    tile = root("road_dirt")
    (Part("Prop", parent=tile)
     .box((0.96, 0.96, 0.03), (0, 0, 0.015), "road_mud", bevel=0.02)
     .cylinder(0.16, 0.008, (0.18, -0.15, 0.034), "wet_mud", segments=7)
     .sphere(0.04, (-0.25, 0.2, 0.035), "fieldstone", segments=5, rings=3, scale=(1.2, 1.0, 0.6))
     .sphere(0.03, (0.3, 0.28, 0.034), "worn_stone", segments=5, rings=3, scale=(1.0, 1.3, 0.6))
     .build())
    return tile
