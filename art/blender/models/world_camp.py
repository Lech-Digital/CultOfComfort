"""A rival cult's camp on the world map: hide tents round a smoky fire and a tall pole whose banner Unity dyes in the
cult's colour (the `Banner` part). They also believe in comfort. Just a different kind."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 1800
REQUIRED_PARTS = ("Building", "Banner")

_TENTS = ((-0.42, 0.2, 25), (0.4, 0.28, -30), (0.05, 0.52, 90))


def build():
    camp = root("world_camp")
    b = Part("Building", parent=camp)
    b.cylinder(0.85, 0.04, (0, 0, 0.02), "wet_mud", segments=12)
    for x, y, yaw in _TENTS:
        b.prism((0.42, 0.5, 0.36), (x, y, 0.2), "levy_wool", rotation=(0, 0, yaw))
        b.box((0.03, 0.03, 0.42), (x, y, 0.21), "old_wood", bevel=0.0)
    for k in range(7):                                                                                     # fire ring
        a = k * math.tau / 7
        b.sphere(0.045, (math.cos(a) * 0.14, -0.15 + math.sin(a) * 0.14, 0.05), "fieldstone", segments=6, rings=4)
    b.cylinder(0.08, 0.12, (0, -0.15, 0.08), "fire", segments=6, radius_top=0.0)
    b.sphere(0.06, (0.03, -0.12, 0.3), "linen", segments=6, rings=4, scale=(1, 1, 0.7))                 # smoke
    b.sphere(0.045, (0.07, -0.1, 0.42), "linen", segments=6, rings=4, scale=(1, 1, 0.7))
    b.cylinder(0.022, 1.1, (-0.3, -0.38, 0.55), "old_wood", segments=5)                                  # banner pole
    b.build()
    banner = Part("Banner", pivot=(-0.3, -0.38, 1.0), parent=camp)
    banner.box((0.03, 0.26, 0.34), (-0.3, -0.25, 0.93), "linen", bevel=0.005)
    banner.box((0.035, 0.1, 0.1), (-0.3, -0.25, 0.95), "linen", bevel=0.003)
    banner.build()
    return camp
