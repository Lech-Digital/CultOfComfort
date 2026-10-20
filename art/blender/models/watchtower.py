"""The Watchtower: four tall poles lashed together, a platform with a rail, a roof of planks, a ladder, a flag, and a
spyglass that is really a rolled-up hymn sheet. From up there you can see two Nights coming."""
from lib.buildings import plank_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1600
REQUIRED_PARTS = ("Building", "Flag")


def build():
    tower = root("watchtower")
    b = Part("Building", parent=tower)
    b.box((0.7, 0.7, 0.05), (0, 0, 0.025), "worn_stone", bevel=0.01)
    for x, y in ((-0.22, -0.22), (0.22, -0.22), (-0.22, 0.22), (0.22, 0.22)):
        b.cylinder(0.04, 2.0, (x * 0.9, y * 0.9, 1.0), "old_wood", segments=5, rotation=(-y * 4, x * 4, 0))
    for z in (0.6, 1.2):
        b.box((0.5, 0.03, 0.03), (0, -0.2, z), "fresh_log", bevel=0.0, rotation=(0, 35 if z < 1 else -35, 0))   # braces
    b.box((0.62, 0.62, 0.05), (0, 0, 1.8), "fresh_log", bevel=0.008)                                          # platform
    for y in (-0.3, 0.3):
        b.box((0.62, 0.03, 0.03), (0, y, 2.0), "old_wood", bevel=0.0)
    for x in (-0.3, 0.3):
        b.box((0.03, 0.62, 0.03), (x, 0, 2.0), "old_wood", bevel=0.0)
    plank_roof(b, 0.7, 0.7, (0, 0, 2.35), 25)
    for k in range(6):
        b.box((0.2, 0.025, 0.025), (0.3, -0.33, 0.2 + k * 0.28), "fresh_log", bevel=0.0)                       # ladder rungs
    for x in (0.2, 0.4):
        b.box((0.025, 0.025, 1.75), (x, -0.34, 0.88), "old_wood", bevel=0.0)
    b.cylinder(0.025, 0.22, (0.12, -0.28, 2.1), "linen", segments=6, rotation=(0, 80, 20))                    # the spyglass
    building = b.build()
    (Part("Flag", pivot=(-0.28, -0.28, 2.5), parent=building)
     .cylinder(0.015, 0.45, (-0.3, -0.3, 2.6), "old_wood", segments=4)
     .box((0.22, 0.02, 0.14), (-0.18, -0.3, 2.75), "zealot_red", bevel=0.0)
     .build())
    return tower
