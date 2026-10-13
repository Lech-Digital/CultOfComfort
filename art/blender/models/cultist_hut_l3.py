"""Cultist Hut, level 3 — the Chapel of Mild Warmth: a round daub chapel under a cone of thatch, a gold spire and,
on top, the Duck Ascendant. Two wings (one thatched, one in a hurry), red banners, a sauna pipe that won't stop
puffing and a hot tub out front. Devotion has never been so moist."""
from lib.buildings import plank_roof, rubber_duck, thatch_roof, towel_line
from lib.mesh import Part, root

TRIANGLE_BUDGET = 5500
REQUIRED_PARTS = ("Building",)


def build():
    chapel = root("cultist_hut_l3")
    b = Part("Building", parent=chapel)
    b.box((1.6, 1.05, 0.06), (0, 0, 0.03), "grass", bevel=0.02)
    b.box((0.36, 0.3, 0.03), (0, -0.38, 0.075), "worn_stone", bevel=0.01)                               # front step
    # The round chapel.
    b.cylinder(0.32, 0.64, (0, 0.1, 0.38), "daub", segments=12)
    b.cylinder(0.335, 0.05, (0, 0.1, 0.68), "gold_trim", segments=12)
    b.cylinder(0.43, 0.48, (0, 0.1, 0.94), "thatch", segments=12, radius_top=0.0)
    b.cylinder(0.035, 0.24, (0, 0.1, 1.28), "gold_trim", segments=6, radius_top=0.012)
    rubber_duck(b, (0, 0.1, 1.36), size=1.5)                                                            # the Duck Ascendant
    b.box((0.2, 0.06, 0.36), (0, -0.21, 0.24), "tarred_wood", bevel=0.02)                               # door
    b.cylinder(0.07, 0.03, (0, -0.22, 0.52), "window", segments=10, rotation=(90, 0, 0))                # round window
    # Red banners either side of the door.
    for x in (-0.24, 0.24):
        b.cylinder(0.018, 0.85, (x, -0.33, 0.43), "old_wood", segments=5)
        b.box((0.14, 0.015, 0.34), (x, -0.34, 0.6), "zealot_red", bevel=0.003)
        b.box((0.145, 0.016, 0.03), (x, -0.345, 0.5), "gold_trim", bevel=0.002)
    # Left wing, thatched.
    b.box((0.36, 0.5, 0.4), (-0.52, 0.18, 0.26), "daub", bevel=0.03)
    b.box((0.1, 0.03, 0.1), (-0.52, -0.08, 0.32), "window", bevel=0.005)
    thatch_roof(b, 0.32, 0.5, (-0.52, 0.18, 0.46), 0.32)
    # Right wing, in a hurry: plank roof and a sauna pipe.
    b.box((0.36, 0.5, 0.38), (0.53, 0.2, 0.25), "daub", bevel=0.03, rotation=(0, 0, -2))
    plank_roof(b, 0.4, 0.56, (0.53, 0.2, 0.47), -12)
    b.cylinder(0.04, 0.5, (0.62, 0.32, 0.72), "gold_trim", segments=6)
    for k, (r, z) in enumerate(((0.07, 1.02), (0.055, 1.14), (0.04, 1.24))):
        b.sphere(r, (0.62 + k * 0.03, 0.32, z), "linen", segments=6, rings=4)
    # Hot tub, steaming.
    b.cylinder(0.15, 0.15, (-0.5, -0.32, 0.135), "old_wood", segments=10)
    b.cylinder(0.155, 0.02, (-0.5, -0.32, 0.17), "fieldstone", segments=10)
    b.cylinder(0.125, 0.02, (-0.5, -0.32, 0.2), "water", segments=10)
    rubber_duck(b, (-0.45, -0.34, 0.19), size=0.3)
    for k, (dx, z) in enumerate(((-0.03, 0.3), (0.03, 0.4))):
        b.sphere(0.05 - k * 0.01, (-0.5 + dx, -0.32, z), "linen", segments=6, rings=4)
    towel_line(b, (0.32, -0.44), (0.74, -0.32), 0.5, ("tarp_blue", "linen", "flipflop_pink"))
    b.build()
    return chapel
