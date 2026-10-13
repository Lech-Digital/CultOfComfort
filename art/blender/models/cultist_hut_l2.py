"""Cultist Hut, level 2 — the Towel Lodge: the hut sprouts an annex under a plank roof, a rubber duck takes its seat
on the ridge, a long line of towels in every colour of the faith, a tub by the door and flip-flops on the step."""
from lib.buildings import flip_flops, plank_roof, rubber_duck, thatch_roof, towel_line
from lib.mesh import Part, root

TRIANGLE_BUDGET = 4500
REQUIRED_PARTS = ("Building",)


def build():
    lodge = root("cultist_hut_l2")
    b = Part("Building", parent=lodge)
    b.box((1.55, 1.04, 0.06), (0, 0, 0.03), "grass", bevel=0.02)
    # Main hut.
    b.box((0.72, 0.6, 0.52), (-0.28, 0.12, 0.32), "daub", bevel=0.03, rotation=(0, 0, 2))
    for x in (-0.64, 0.08):
        b.box((0.06, 0.62, 0.54), (x, 0.12, 0.32), "old_wood", bevel=0.01, rotation=(0, 0, 2))
    thatch_roof(b, 0.72, 0.6, (-0.28, 0.12, 0.58), 0.52)
    rubber_duck(b, (-0.28, 0.12, 1.08), size=1.0)
    b.box((0.2, 0.04, 0.34), (-0.3, -0.19, 0.23), "tarred_wood", bevel=0.01)                           # door
    b.box((0.12, 0.03, 0.1), (-0.52, -0.19, 0.4), "window", bevel=0.005)
    flip_flops(b, (-0.3, -0.28, 0.06), yaw=10)
    # The annex.
    b.box((0.44, 0.48, 0.4), (0.38, 0.2, 0.26), "daub", bevel=0.03, rotation=(0, 0, -3))
    b.box((0.1, 0.03, 0.1), (0.38, -0.05, 0.32), "window", bevel=0.005)
    plank_roof(b, 0.5, 0.56, (0.38, 0.2, 0.5), -14)
    # Towels, in every colour of the faith.
    towel_line(b, (0.15, -0.44), (0.74, -0.2), 0.62, ("linen", "tarp_blue", "flipflop_pink", "hivis_yellow", "linen"))
    # A tub with a duck in it.
    b.box((0.32, 0.18, 0.13), (-0.52, -0.36, 0.13), "old_wood", bevel=0.03, bevel_segments=2)
    b.box((0.27, 0.13, 0.02), (-0.52, -0.36, 0.19), "water", bevel=0.005)
    rubber_duck(b, (-0.47, -0.36, 0.17), size=0.35)
    b.build()
    return lodge
