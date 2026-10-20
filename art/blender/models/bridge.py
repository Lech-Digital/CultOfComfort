"""One span of a plank bridge, a tile long (along x): a deck of boards over two beams, rails on posts, and piles going
down into the water. Spans in a row make a bridge; the raiders cross it, the cult will want to hold it."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    bridge = root("bridge")
    p = Part("Prop", parent=bridge)
    for y in (-0.3, 0.3):
        p.box((1.0, 0.08, 0.1), (0, y, 0.12), "tarred_wood", bevel=0.01)                                            # beams
    for k in range(6):
        x = -0.42 + k * 0.168
        p.box((0.15, 0.82, 0.04), (x, 0, 0.19), "old_wood" if k % 2 else "fresh_log", bevel=0.008)                 # boards
    for y in (-0.42, 0.42):
        for x in (-0.45, 0.45):
            p.cylinder(0.035, 0.42, (x, y, 0.36), "old_wood", segments=5)                                            # posts
            p.cylinder(0.05, 0.6, (x, y, -0.2), "tarred_wood", segments=5)                                            # piles
        p.box((1.0, 0.05, 0.05), (0, y, 0.52), "fresh_log", bevel=0.008)                                            # rail
    p.build()
    return bridge
