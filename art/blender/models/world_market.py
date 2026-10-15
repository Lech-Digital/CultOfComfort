"""A market square on the world map: a stone market cross, striped stalls in woad and madder, barrels, and somebody's
goose that won't leave."""
import math

from lib.buildings import barrel
from lib.mesh import Part, root

TRIANGLE_BUDGET = 2500
REQUIRED_PARTS = ("Building",)

_STALLS = ((-0.42, 0.18, 20, "madder"), (0.38, 0.3, -25, "woad"), (0.05, -0.42, 175, "weld"))


def build():
    market = root("world_market")
    b = Part("Building", parent=market)
    b.cylinder(0.75, 0.04, (0, 0, 0.02), "road_mud", segments=12)                                        # the trodden square
    b.cylinder(0.12, 0.1, (0, 0, 0.09), "fieldstone", segments=8)                                        # market cross
    b.box((0.05, 0.05, 0.42), (0, 0, 0.35), "worn_stone", bevel=0.006)
    b.box((0.2, 0.05, 0.05), (0, 0, 0.5), "worn_stone", bevel=0.006)
    for x, y, yaw, colour in _STALLS:
        a = math.radians(yaw)
        c, s = math.cos(a), math.sin(a)
        def at(dx, dy):
            return (x + dx * c - dy * s, y + dx * s + dy * c)
        b.box((0.36, 0.2, 0.12), (*at(0, 0), 0.12), "fresh_log", bevel=0.01, rotation=(0, 0, yaw))       # counter
        for dx in (-0.16, 0.16):
            for dy in (-0.09, 0.09):
                b.box((0.03, 0.03, 0.36), (*at(dx, dy), 0.2), "old_wood", bevel=0.0, rotation=(0, 0, yaw))
        b.box((0.42, 0.28, 0.03), (*at(0, -0.02), 0.39), colour, bevel=0.005, rotation=(-12, 0, yaw))     # awning
        b.box((0.42, 0.02, 0.06), (*at(0, -0.16), 0.34), "linen", bevel=0.004, rotation=(0, 0, yaw))
        b.sphere(0.04, (*at(-0.08, 0), 0.2), "ochre_clay", segments=6, rings=4)                         # wares
        b.sphere(0.035, (*at(0.06, 0.02), 0.2), "foliage", segments=6, rings=4)
    barrel(b, (0.5, -0.25, 0.1), radius=0.07, height=0.18)
    barrel(b, (0.58, -0.1, 0.1), radius=0.06, height=0.16)
    b.box((0.22, 0.12, 0.08), (-0.45, -0.3, 0.06), "thatch", bevel=0.01)                                 # a hay bale
    b.build()
    return market
