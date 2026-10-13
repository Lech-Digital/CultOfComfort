"""Storage Hut, level 2 — the Granary on Stilts: the hut climbs up onto mushroom-shaped staddle stones (rats hate
this one trick), a ladder to the door and a hoist arm with a sack dangling from it."""
from lib.buildings import barrel, log_wall, sack, thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 4000
REQUIRED_PARTS = ("Building",)


def build():
    granary = root("storage_hut_l2")
    b = Part("Building", parent=granary)
    b.box((1.0, 1.0, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    for x in (-0.28, 0.28):                                                                              # staddle stones
        for y in (-0.22, 0.26):
            b.cylinder(0.05, 0.18, (x, y, 0.15), "fieldstone", segments=8, radius_top=0.035)
            b.cylinder(0.1, 0.05, (x, y, 0.26), "worn_stone", segments=10)
    b.box((0.72, 0.64, 0.04), (0, 0.02, 0.3), "old_wood", bevel=0.01)                                   # floor
    corners = ((-0.32, -0.26), (0.32, -0.26), (0.32, 0.3), (-0.32, 0.3))
    for k in range(4):
        log_wall(b, corners[k], corners[(k + 1) % 4], height=0.5, z0=0.32, radius=0.05)
    thatch_roof(b, 0.64, 0.56, (0, 0.02, 0.84), 0.45)
    b.box((0.3, 0.36, 0.02), (-0.2, -0.05, 1.08), "tarp_blue", bevel=0, rotation=(0, -52, 0))           # patched already
    b.box((0.2, 0.04, 0.32), (-0.08, -0.29, 0.5), "tarred_wood", bevel=0.01)                            # door
    for side in (-1, 1):                                                                                 # ladder
        b.box((0.03, 0.03, 0.42), (-0.08 + side * 0.09, -0.4, 0.2), "fresh_log", bevel=0.004, rotation=(-22, 0, 0))
    for k in range(3):
        b.box((0.18, 0.025, 0.025), (-0.08, -0.46 + k * 0.05, 0.1 + k * 0.11), "fresh_log", bevel=0.003)
    # Hoist arm with a sack on a rope.
    b.box((0.05, 0.42, 0.05), (0.26, -0.22, 1.02), "old_wood", bevel=0.006)
    b.cylinder(0.006, 0.3, (0.26, -0.41, 0.86), "thatch", segments=4)
    sack(b, (0.26, -0.41, 0.56))
    barrel(b, (-0.38, -0.38, 0.06), radius=0.1, height=0.26)
    barrel(b, (0.38, 0.36, 0.06), radius=0.09, height=0.24)
    b.box((0.18, 0.18, 0.16), (0.38, -0.36, 0.14), "fresh_log", bevel=0.012, rotation=(0, 0, 18))       # crate
    b.build()
    return granary
