"""A lord's keep on the world map: a timber tower on an earth motte, a palisaded bailey below with a hall, and the
lord's red banner on top. Built to be looked at from far away by people who owe him money."""
import math

from lib.buildings import thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3500
REQUIRED_PARTS = ("Building",)


def build():
    keep = root("world_keep")
    b = Part("Building", parent=keep)
    b.cylinder(0.62, 0.55, (0.3, 0.25, 0.27), "grass", segments=12, radius_top=0.36)                     # motte
    b.box((0.48, 0.48, 0.7), (0.3, 0.25, 0.9), "fresh_log", bevel=0.02)                                  # tower
    for x in (-1, 1):
        for y in (-1, 1):
            b.box((0.07, 0.07, 0.78), (0.3 + x * 0.24, 0.25 + y * 0.24, 0.92), "old_wood", bevel=0.01)
    b.box((0.58, 0.58, 0.08), (0.3, 0.25, 1.28), "old_wood", bevel=0.01)                                 # fighting deck
    for k in range(8):
        a = k * math.tau / 8
        b.box((0.06, 0.06, 0.14), (0.3 + math.cos(a) * 0.27, 0.25 + math.sin(a) * 0.27, 1.38), "tarred_wood", bevel=0.005)
    b.cylinder(0.36, 0.3, (0.3, 0.25, 1.5), "tarred_wood", segments=4, radius_top=0.02, rotation=(0, 0, 45))   # roof
    b.box((0.12, 0.03, 0.12), (0.3, 0.0, 1.0), "window", bevel=0.005)
    b.cylinder(0.015, 0.5, (0.3, 0.25, 1.85), "old_wood", segments=5)                                     # flagpole
    b.box((0.26, 0.02, 0.16), (0.43, 0.25, 1.98), "wulfric_red", bevel=0.004, rotation=(0, 0, -8))
    b.box((0.08, 0.025, 0.08), (0.43, 0.24, 1.98), "gold_trim", bevel=0.003, rotation=(0, 0, -8))
    # The bailey: a ring of stakes, a gate at the front, a hall inside.
    stakes = 26
    for k in range(stakes):
        a = k * math.tau / stakes
        if abs(a - math.radians(250)) < 0.22:
            continue                                                                                        # the gateway
        x, y = math.cos(a) * 1.05, math.sin(a) * 0.95
        b.cylinder(0.035, 0.36 + (k % 3) * 0.03, (x, y, 0.2), "fresh_log" if k % 2 else "old_wood", segments=5, radius_top=0.01)
    gx, gy = math.cos(math.radians(250)) * 1.05, math.sin(math.radians(250)) * 0.95
    for d in (-0.16, 0.16):
        b.box((0.08, 0.08, 0.55), (gx + d, gy, 0.28), "old_wood", bevel=0.01)
    b.box((0.44, 0.08, 0.07), (gx, gy, 0.56), "old_wood", bevel=0.01)
    b.box((0.62, 0.36, 0.3), (-0.42, -0.1, 0.2), "daub", bevel=0.02)                                     # the hall
    thatch_roof(b, 0.62, 0.36, (-0.42, -0.1, 0.35), 0.28, overhang=0.06, lean=0)
    b.box((0.12, 0.03, 0.2), (-0.42, -0.29, 0.15), "tarred_wood", bevel=0.005)
    b.build()
    return keep
