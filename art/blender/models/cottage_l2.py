"""Cottage, level 2 — the Two-Up: a jettied timber-framed upper floor, a dormer, a taller smoking chimney, window
boxes of flowers, a plastic garden chair and flip-flops by the door. Six beds, twelve opinions about the Prophet."""
from lib.buildings import flip_flops, thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3500
REQUIRED_PARTS = ("Building",)


def build():
    cottage = root("cottage_l2")
    b = Part("Building", parent=cottage)
    b.box((1.04, 1.0, 0.05), (0, 0, 0.025), "grass", bevel=0.02)
    b.box((0.8, 0.6, 0.44), (0, 0.06, 0.27), "daub", bevel=0.03)                                        # ground floor
    for x in (-0.4, 0.4):
        b.box((0.06, 0.62, 0.46), (x, 0.06, 0.27), "old_wood", bevel=0.01)
    b.box((0.2, 0.04, 0.32), (-0.14, -0.25, 0.21), "tarred_wood", bevel=0.01)                           # door
    b.box((0.14, 0.03, 0.12), (0.2, -0.25, 0.3), "window", bevel=0.005)
    # The jettied upper floor with its timber frame.
    b.box((0.88, 0.7, 0.05), (0, 0.06, 0.51), "old_wood", bevel=0.01)
    b.box((0.86, 0.68, 0.36), (0, 0.06, 0.71), "daub", bevel=0.03)
    for x in (-0.43, 0.0, 0.43):
        b.box((0.05, 0.7, 0.38), (x, 0.06, 0.71), "old_wood", bevel=0.008)
    for x, side in ((-0.22, 1), (0.22, -1)):
        b.box((0.035, 0.03, 0.44), (x, -0.29, 0.71), "old_wood", bevel=0.004, rotation=(0, side * 40, 0))
    for x in (-0.3, 0.3):
        b.box((0.13, 0.03, 0.12), (x, -0.29, 0.76), "window", bevel=0.005)
        b.box((0.16, 0.06, 0.04), (x, -0.32, 0.68), "old_wood", bevel=0.005)                            # window box
        for k in range(3):
            b.sphere(0.025, (x + (k - 1) * 0.045, -0.32, 0.72), "flipflop_pink" if k % 2 else "foliage", segments=5, rings=3)
    thatch_roof(b, 0.82, 0.66, (0, 0.06, 0.89), 0.5)
    # Dormer on the front slope.
    b.box((0.2, 0.18, 0.16), (0.02, -0.08, 1.02), "daub", bevel=0.01)
    b.prism((0.26, 0.24, 0.12), (0.02, -0.08, 1.16), "thatch")
    b.box((0.1, 0.02, 0.08), (0.02, -0.17, 1.03), "window", bevel=0.004)
    b.box((0.14, 0.14, 0.62), (0.28, 0.25, 1.15), "fieldstone", bevel=0.02)                             # chimney
    for k, (r, z) in enumerate(((0.07, 1.52), (0.055, 1.64))):
        b.sphere(r, (0.3 + k * 0.03, 0.24, z), "linen", segments=6, rings=4, scale=(1.0, 1.0, 0.8))
    # Garden chair and flip-flops.
    b.box((0.16, 0.15, 0.03), (0.32, -0.4, 0.14), "linen", bevel=0.008)
    b.box((0.16, 0.03, 0.16), (0.32, -0.33, 0.22), "linen", bevel=0.008, rotation=(-12, 0, 0))
    for dx in (-0.065, 0.065):
        for dy in (-0.06, 0.06):
            b.box((0.02, 0.02, 0.13), (0.32 + dx, -0.4 + dy, 0.07), "linen", bevel=0.003)
    flip_flops(b, (-0.14, -0.34, 0.05), yaw=-12)
    b.build()
    return cottage
