"""Storage Hut, level 3 — the Hoard Tower: a log ground floor, a jettied daub storey on top, a pointed thatch hat of a roof
with a cult pennant, a wobbly stack of crates and a crane jib from which a wicker trolley dangles. Nobody remembers
what is in it. Nobody dares to look."""
from lib.buildings import barrel, log_wall, sack, wicker_trolley
from lib.mesh import Part, root

TRIANGLE_BUDGET = 5000
REQUIRED_PARTS = ("Building",)


def build():
    tower = root("storage_hut_l3")
    b = Part("Building", parent=tower)
    b.box((1.04, 1.04, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    corners = ((-0.32, -0.26), (0.32, -0.26), (0.32, 0.32), (-0.32, 0.32))
    for k in range(4):
        log_wall(b, corners[k], corners[(k + 1) % 4], height=0.5, z0=0.06, radius=0.05)
    b.box((0.22, 0.04, 0.36), (0.08, -0.29, 0.24), "tarred_wood", bevel=0.01)                           # door
    # The jettied upper storey.
    b.box((0.78, 0.68, 0.06), (0, 0.03, 0.6), "old_wood", bevel=0.01)
    b.box((0.76, 0.66, 0.42), (0, 0.03, 0.84), "daub", bevel=0.03, rotation=(0, 0, 2))
    for x in (-0.38, 0.38):
        b.box((0.06, 0.68, 0.44), (x, 0.03, 0.84), "old_wood", bevel=0.01, rotation=(0, 0, 2))
    for side in (-1, 1):                                                                                 # cross bracing
        b.box((0.04, 0.03, 0.5), (side * 0.18, -0.31, 0.84), "old_wood", bevel=0.004, rotation=(0, side * 38, 0))
    b.box((0.12, 0.03, 0.12), (0.0, -0.32, 0.9), "window", bevel=0.005)
    b.cylinder(0.62, 0.75, (0, 0.03, 1.43), "thatch", segments=4, radius_top=0.0, rotation=(0, 0, 45))   # pointed hat roof
    b.box((0.3, 0.3, 0.02), (0.15, -0.12, 1.3), "woad", bevel=0, rotation=(-30, 28, 10))               # woad cloth, of course
    b.cylinder(0.02, 0.45, (0, 0.03, 1.95), "old_wood", segments=5)                                     # pennant
    b.box((0.22, 0.012, 0.13), (0.11, 0.03, 2.08), "zealot_red", bevel=0.003)
    # Crane jib with a dangling wicker trolley.
    b.box((0.05, 0.5, 0.05), (-0.3, -0.3, 1.08), "old_wood", bevel=0.006)
    b.box((0.04, 0.04, 0.3), (-0.3, -0.18, 0.95), "old_wood", bevel=0.005, rotation=(-40, 0, 0))
    b.cylinder(0.006, 0.38, (-0.3, -0.52, 0.88), "thatch", segments=4)
    wicker_trolley(b, (-0.3, -0.45, 0.42), yaw=10)
    sack(b, (-0.3, -0.45, 0.58), swatch="levy_wool")
    # A wobbly crate tower.
    for k, (dx, yaw) in enumerate(((0, 8), (0.03, -10), (-0.02, 20))):
        b.box((0.2, 0.2, 0.18), (0.38 + dx, -0.36, 0.15 + k * 0.18), "fresh_log", bevel=0.012, rotation=(0, k * 2, yaw))
    barrel(b, (-0.42, 0.38, 0.06), radius=0.09, height=0.24)
    barrel(b, (0.42, 0.4, 0.06), radius=0.1, height=0.26)
    b.build()
    return tower
