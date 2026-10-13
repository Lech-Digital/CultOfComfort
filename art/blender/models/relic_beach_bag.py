"""Relic: the beach bag from the back seat: flip-flops, a towel, sun cream, and the rubber duck that used to sit on
the dashboard. Everything the Cult of Comfort believes in came out of this bag."""
from lib.buildings import rubber_duck
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    relic = root("relic_beach_bag")
    p = Part("Prop", parent=relic)
    p.box((0.34, 0.16, 0.24), (0, 0, 0.13), "flipflop_pink", bevel=0.04, bevel_segments=2)
    for x in (-0.09, 0.09):
        p.box((0.02, 0.02, 0.16), (x, 0, 0.3), "linen", bevel=0.005, rotation=(0, (x > 0) * -50 + 25, 0))
    p.box((0.2, 0.14, 0.05), (-0.05, 0.02, 0.27), "tarp_blue", bevel=0.02, rotation=(0, 10, 0))      # the towel
    p.cylinder(0.025, 0.1, (0.1, 0.02, 0.29), "linen", segments=8)                                     # sun cream
    rubber_duck(p, (0.02, -0.01, 0.24), size=0.6, relic=True)
    p.build()
    return relic
