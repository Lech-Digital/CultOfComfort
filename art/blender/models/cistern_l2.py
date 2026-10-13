"""Cistern, level 2 — the Raised Cistern (tech tree: upgrades raise the cistern; every 10 m of head is a bar).
A barrel tank on a braced timber tower, a ladder, a leather hose ending in a hi-vis nozzle, and a rubber duck
floating on top. Lifeguard on duty: the duck."""
from lib.buildings import rubber_duck
from lib.mesh import Part, root

TRIANGLE_BUDGET = 4000
REQUIRED_PARTS = ("Building",)


def build():
    cistern = root("cistern_l2")
    b = Part("Building", parent=cistern)
    b.cylinder(0.42, 0.08, (0, 0, 0.04), "fieldstone", segments=12)                                      # drip basin
    b.cylinder(0.36, 0.02, (0, 0, 0.085), "water", segments=12)
    for x in (-0.3, 0.3):                                                                                  # tower legs
        for y in (-0.3, 0.3):
            b.box((0.07, 0.07, 1.0), (x, y, 0.5), "old_wood", bevel=0.01, rotation=(-y * 8, x * 8, 0))
    for side in (-1, 1):                                                                                  # cross bracing
        b.box((0.04, 0.03, 0.8), (0, -0.31, 0.5), "old_wood", bevel=0.004, rotation=(0, side * 34, 0))
        b.box((0.03, 0.04, 0.8), (0.31, 0, 0.5), "old_wood", bevel=0.004, rotation=(side * 34, 0, 0))
    b.box((0.72, 0.72, 0.05), (0, 0, 1.0), "old_wood", bevel=0.01)                                       # platform
    # The tank: a big barrel.
    b.cylinder(0.38, 0.46, (0, 0, 1.25), "old_wood", segments=14, radius_top=0.36)
    for z in (1.1, 1.38):
        b.cylinder(0.39, 0.03, (0, 0, z), "fieldstone", segments=14)
    b.cylinder(0.34, 0.02, (0, 0, 1.47), "water", segments=14)
    rubber_duck(b, (0.1, 0.05, 1.45), size=0.7)
    # Ladder up the left side.
    for y in (-0.1, 0.1):
        b.box((0.03, 0.03, 1.05), (-0.42, y, 0.52), "fresh_log", bevel=0.004, rotation=(0, -8, 0))
    for k in range(5):
        b.box((0.025, 0.22, 0.025), (-0.43 + k * 0.016, 0, 0.15 + k * 0.2), "fresh_log", bevel=0.003)
    # Leather hose down to a hi-vis nozzle.
    b.cylinder(0.035, 0.5, (0.22, -0.3, 0.78), "tarred_wood", segments=6, rotation=(20, 0, 0))
    b.cylinder(0.035, 0.45, (0.25, -0.4, 0.33), "tarred_wood", segments=6, rotation=(-10, 10, 0))
    b.cylinder(0.035, 0.25, (0.25, -0.42, 0.09), "tarred_wood", segments=6, rotation=(80, 0, 0))
    b.cylinder(0.045, 0.12, (0.25, -0.52, 0.08), "hivis_yellow", segments=8, radius_top=0.025, rotation=(90, 0, 0))
    b.build()
    return cistern
