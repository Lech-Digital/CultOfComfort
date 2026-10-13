"""Peat Cut, level 3 — the Peat Works: a deep cut with a windlass and bucket, a tall smoking drying barn, a peat
pyramid crowned with a clay cone, and a wicker copy of the Holy Trolley pressed into service as a mine cart."""
from lib.buildings import clay_cone, peat_stack, thatch_roof, wheel, wicker_trolley
from lib.mesh import Part, root

TRIANGLE_BUDGET = 5000
REQUIRED_PARTS = ("Building",)


def build():
    works = root("peat_cut_l3")
    b = Part("Building", parent=works)
    b.box((1.06, 2.12, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    b.box((0.48, 0.9, 0.05), (-0.24, -0.45, 0.065), "wet_mud", bevel=0.02, bevel_segments=2)            # the deep cut
    b.box((0.44, 0.86, 0.02), (-0.24, -0.45, 0.085), "water", bevel=0.01)
    # Windlass over the cut.
    for x in (-0.46, -0.02):
        b.box((0.05, 0.05, 0.85), (x, -0.45, 0.45), "old_wood", bevel=0.006, rotation=(0, (x < -0.2) * 16 - 8, 0))
    b.cylinder(0.035, 0.56, (-0.24, -0.45, 0.85), "tarred_wood", segments=6, rotation=(0, 90, 0))
    wheel(b, (0.05, -0.45, 0.85), 0.16, spokes=4, rim=8, width=0.04)
    b.cylinder(0.006, 0.45, (-0.24, -0.45, 0.6), "thatch", segments=4)
    b.cylinder(0.07, 0.11, (-0.24, -0.45, 0.33), "old_wood", segments=8, radius_top=0.085)              # bucket
    # The drying barn: daub walls, a tall thatch roof, a peat-smoke chimney.
    b.box((0.8, 0.7, 0.62), (0.05, 0.6, 0.37), "daub", bevel=0.03)
    for x in (-0.35, 0.45):
        b.box((0.06, 0.72, 0.64), (x, 0.6, 0.37), "old_wood", bevel=0.01)
    for k in range(4):                                                                                   # open drying slats
        b.box((0.08, 0.03, 0.5), (-0.18 + k * 0.14, 0.24, 0.38), "old_wood", bevel=0.004)
    thatch_roof(b, 0.8, 0.7, (0.05, 0.6, 0.68), 0.62)
    b.box((0.16, 0.16, 0.5), (0.28, 0.82, 1.2), "fieldstone", bevel=0.02)
    for k, (r, z) in enumerate(((0.08, 1.52), (0.06, 1.66), (0.045, 1.78))):
        b.sphere(r, (0.3 + k * 0.03, 0.8, z), "linen", segments=6, rings=4)
    # Peat pyramid with its cone.
    for row, n in enumerate((4, 3, 2, 1)):
        for k in range(n):
            b.box((0.16, 0.16, 0.08), (0.25 + (k - (n - 1) / 2) * 0.13, -0.6, 0.1 + row * 0.085), "tarred_wood" if (k + row) % 2 else "wet_mud",
                  bevel=0.01, rotation=(0, 0, row * 12))
    clay_cone(b, (0.25, -0.6, 0.4), size=0.8)
    # The wicker trolley, full of peat.
    wicker_trolley(b, (0.27, -0.12, 0.06), yaw=8, load="tarred_wood")
    peat_stack(b, (-0.3, 0.2, 0.06), rows=4)
    peat_stack(b, (-0.3, -0.02, 0.06), rows=3, yaw=20)
    b.build()
    return works
