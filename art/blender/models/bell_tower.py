"""The Bell Tower: a stone base and a timber belfry with a pitched roof and a big bronze bell, rope hanging down.
When it rings, every cultist with a broom comes running."""
from lib.buildings import plank_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1800
REQUIRED_PARTS = ("Building", "Bell")


def build():
    tower = root("bell_tower")
    b = Part("Building", parent=tower)
    b.box((1.2, 1.2, 0.06), (0, 0, 0.03), "worn_stone", bevel=0.02)
    b.box((0.8, 0.8, 0.9), (0, 0, 0.5), "fieldstone", bevel=0.03)                                              # stone base
    b.box((0.26, 0.04, 0.42), (0, -0.41, 0.25), "tarred_wood", bevel=0.01)                                     # door
    for x, y in ((-0.32, -0.32), (0.32, -0.32), (-0.32, 0.32), (0.32, 0.32)):
        b.box((0.08, 0.08, 0.7), (x, y, 1.3), "old_wood", bevel=0.01)                                          # belfry posts
    b.box((0.8, 0.8, 0.06), (0, 0, 0.98), "fresh_log", bevel=0.008)
    plank_roof(b, 0.95, 0.95, (0, 0, 1.85), 32)
    b.cylinder(0.012, 0.9, (0, 0, 1.0), "thatch", segments=4)                                                  # bell rope
    building = b.build()
    (Part("Bell", pivot=(0, 0, 1.6), parent=building)
     .cylinder(0.2, 0.32, (0, 0, 1.42), "gold_trim", segments=10, radius_top=0.11)
     .sphere(0.04, (0, 0, 1.25), "mail", segments=6, rings=4)
     .build())
    return tower
