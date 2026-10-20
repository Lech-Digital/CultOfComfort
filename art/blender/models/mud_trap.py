"""A mud trap: a shallow pit of sucking mud dug into the road, a few sharpened sticks round the edge, a scatter of
straw to hide it. Laid at dusk; the first boots to find it stay found."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    trap = root("mud_trap")
    p = Part("Prop", parent=trap)
    p.cylinder(0.42, 0.04, (0, 0, 0.0), "road_mud", segments=10)                                            # the rim
    p.sphere(0.36, (0, 0, 0.0), "wet_mud", segments=10, rings=4, scale=(1.0, 1.0, 0.12), smooth=True)       # the pit
    for k in range(6):
        import math
        a = k * math.pi / 3 + 0.3
        x, y = math.cos(a) * 0.4, math.sin(a) * 0.4
        p.cylinder(0.018, 0.22, (x, y, 0.08), "fresh_log", segments=4, radius_top=0.0,
                   rotation=(-math.sin(a) * 25, math.cos(a) * 25, 0))                                          # stakes
    for x, y, yaw in ((0.1, 0.12, 30), (-0.15, -0.05, 100), (0.02, -0.18, 160)):
        p.box((0.22, 0.02, 0.01), (x, y, 0.035), "thatch", bevel=0.0, rotation=(0, 0, yaw))                 # straw
    p.build()
    return trap
