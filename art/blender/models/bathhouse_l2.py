"""Bathhouse, level 2: the Thermae of Mild Warmth. Roman revival, badly remembered: a daub dome on columns over a
bigger pool, steam vents, a hot tub for the devout, and on top of the dome the second-biggest carved duck in the
Marchlands (the Chapel's is bigger; there was a meeting about it)."""
from lib.buildings import flip_flops, rubber_duck, towel_line
from lib.mesh import Part, root

TRIANGLE_BUDGET = 7000
REQUIRED_PARTS = ("Building",)


def build():
    thermae = root("bathhouse_l2")
    b = Part("Building", parent=thermae)
    b.box((1.06, 1.06, 0.06), (0, 0, 0.03), "worn_stone", bevel=0.02)
    b.box((1.0, 0.05, 0.05), (0, -0.5, 0.085), "roman_marble", bevel=0.01)
    # The domed hall.
    b.cylinder(0.34, 0.45, (0, 0.18, 0.285), "daub", segments=14)
    for k in range(8):                                                                                       # columns round it
        import math
        a = math.radians(k * 45 + 22.5)
        b.cylinder(0.035, 0.46, (math.cos(a) * 0.38, 0.18 + math.sin(a) * 0.38, 0.29), "roman_marble", segments=8)
    b.cylinder(0.42, 0.04, (0, 0.18, 0.53), "roman_marble", segments=16)
    b.sphere(0.36, (0, 0.18, 0.53), "daub", segments=14, rings=7, scale=(1.0, 1.0, 0.75))
    b.cylinder(0.37, 0.03, (0, 0.18, 0.55), "gold_trim", segments=16)
    rubber_duck(b, (0, 0.18, 0.78), size=1.1)
    b.box((0.16, 0.04, 0.26), (0, -0.17, 0.19), "tarred_wood", bevel=0.01)
    for x in (-0.12, 0.12):
        b.cylinder(0.035, 0.02, (x, -0.165, 0.36), "window", segments=8, rotation=(90, 0, 0))
    # Steam vents.
    for x, y in ((-0.38, 0.42), (0.38, 0.42)):
        b.cylinder(0.04, 0.3, (x, y, 0.21), "gold_trim", segments=6)
        for k in range(3):
            b.sphere(0.05 - k * 0.01, (x, y, 0.42 + k * 0.08), "linen", segments=6, rings=4)
    # The big pool and a hot tub.
    b.box((0.8, 0.3, 0.06), (-0.05, -0.33, 0.09), "roman_marble", bevel=0.015)
    b.box((0.72, 0.22, 0.02), (-0.05, -0.33, 0.12), "water", bevel=0.005)
    for k in range(5):
        b.sphere(0.045, (-0.35 + k * 0.15, -0.33, 0.18 + (k % 2) * 0.04), "linen", segments=6, rings=4)
    b.cylinder(0.1, 0.1, (0.43, -0.05, 0.11), "old_wood", segments=10)
    b.cylinder(0.085, 0.01, (0.43, -0.05, 0.165), "water", segments=10)
    rubber_duck(b, (0.45, -0.06, 0.15), size=0.25)
    towel_line(b, (-0.5, -0.05), (-0.3, 0.05), 0.32, ("madder", "woad"))
    flip_flops(b, (0.15, -0.2, 0.06), yaw=20)
    b.build()
    return thermae
