"""The sauna: a squat log cabin, steam pouring from the roof vent, a bench and a birch whisk by the door.
The cult's second most sacred building (after the Hearth)."""
from lib.buildings import log_wall, thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building", "Steam")


def build():
    sauna = root("sauna")
    b = Part("Building", parent=sauna)
    b.box((1.2, 1.1, 0.06), (0, 0, 0.03), "worn_stone", bevel=0.02)
    corners = ((-0.45, -0.35), (0.45, -0.35), (0.45, 0.4), (-0.45, 0.4))
    for k in range(4):
        log_wall(b, corners[k], corners[(k + 1) % 4], height=0.55, z0=0.06, radius=0.055)
    thatch_roof(b, 0.9, 0.75, (0, 0.025, 0.64), 0.38, lean=-1)
    b.box((0.16, 0.16, 0.25), (0.2, 0.15, 1.05), "fieldstone", bevel=0.015)                             # vent
    b.box((0.22, 0.04, 0.4), (-0.1, -0.37, 0.26), "tarred_wood", bevel=0.01)                            # door
    b.box((0.5, 0.14, 0.04), (0.25, -0.5, 0.2), "fresh_log", bevel=0.01)                                # bench
    for x in (0.05, 0.45):
        b.box((0.04, 0.12, 0.18), (x, -0.5, 0.09), "old_wood", bevel=0.005)
    b.cylinder(0.012, 0.2, (0.33, -0.5, 0.31), "fresh_log", segments=4, rotation=(0, 70, 0))            # birch whisk
    b.sphere(0.06, (0.4, -0.5, 0.29), "foliage", segments=6, rings=4, smooth=True)
    building = b.build()
    (Part("Steam", pivot=(0.2, 0.15, 1.18), parent=building)
     .sphere(0.12, (0.2, 0.15, 1.3), "linen", segments=7, rings=5, smooth=True)
     .sphere(0.09, (0.27, 0.2, 1.46), "linen", segments=6, rings=4, smooth=True)
     .build())
    return sauna
