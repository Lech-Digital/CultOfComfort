"""Storage Hut: a small log hut with a thatched roof, barrels, sacks and a crate outside."""
from lib.buildings import barrel, log_wall, sack, thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    hut = root("storage_hut")
    b = Part("Building", parent=hut)
    b.box((1.2, 1.0, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    corners = ((-0.4, -0.3), (0.4, -0.3), (0.4, 0.35), (-0.4, 0.35))
    for k in range(4):
        log_wall(b, corners[k], corners[(k + 1) % 4], height=0.5, z0=0.06, radius=0.05)
    thatch_roof(b, 0.8, 0.65, (0, 0.025, 0.58), 0.45)
    b.box((0.22, 0.04, 0.36), (0.05, -0.32, 0.24), "tarred_wood", bevel=0.01)                          # door
    barrel(b, (-0.5, -0.4, 0.06))
    barrel(b, (-0.25, -0.5, 0.06), radius=0.1, height=0.26)
    sack(b, (0.42, -0.45, 0.06))
    sack(b, (0.55, -0.3, 0.06), swatch="levy_wool")
    b.box((0.22, 0.22, 0.2), (0.55, 0.1, 0.16), "fresh_log", bevel=0.015, rotation=(0, 0, 12))        # crate
    b.build()
    return hut
