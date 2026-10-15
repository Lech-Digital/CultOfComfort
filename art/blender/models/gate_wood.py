"""Wooden Gate: two stout posts and a pair of plank doors with a cross-brace. Keeps out goats. Mostly."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 700
REQUIRED_PARTS = ("Prop", "DoorL", "DoorR")  # the leaves swing on their hinges when friends come through


def build():
    gate = root("gate_wood")
    p = Part("Prop", parent=gate)
    for x in (-0.47, 0.47):
        p.box((0.12, 0.14, 1.15), (x, 0, 0.575), "old_wood", bevel=0.015)
        p.cylinder(0.07, 0.12, (x, 0, 1.2), "fresh_log", segments=6, radius_top=0.0)
    p.build()
    for side, name in ((-1, "DoorL"), (1, "DoorR")):
        x = side * 0.21
        d = Part(name, pivot=(side * 0.41, 0, 0), parent=gate)                                   # pivot on the hinge
        d.box((0.4, 0.07, 0.85), (x, 0, 0.5), "fresh_log", bevel=0.01)                       # door leaf
        d.box((0.42, 0.08, 0.06), (x, -0.01, 0.3), "tarred_wood", bevel=0.005)
        d.box((0.42, 0.08, 0.06), (x, -0.01, 0.72), "tarred_wood", bevel=0.005)
        d.box((0.05, 0.08, 0.5), (x, -0.012, 0.51), "tarred_wood", bevel=0.005, rotation=(0, side * 38, 0))  # brace
        d.build()
    return gate
