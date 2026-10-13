"""Woodcutter, level 3 — the Treadwheel Sawmill: a great wooden treadwheel, run (enthusiastically, endlessly) by a
carved duck, drives a circular saw under a plank roof. Stacked planks, a woad pennant on a pole, a clay cone, a
weld vest. Nobody asks how a wooden duck turns it. Faith."""
from lib.buildings import clay_cone, plank_roof, rubber_duck, weld_vest, wheel
from lib.mesh import Part, root

TRIANGLE_BUDGET = 5000
REQUIRED_PARTS = ("Building",)


def build():
    mill = root("woodcutter_l3")
    b = Part("Building", parent=mill)
    b.box((2.05, 1.04, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)
    # The treadwheel on its frame, and its runner.
    wheel(b, (-0.45, 0.05, 0.62), 0.5, spokes=8, rim=16, width=0.3)
    for y in (-0.16, 0.26):
        for side in (-1, 1):
            b.box((0.06, 0.06, 0.7), (-0.45 + side * 0.17, y, 0.33), "old_wood", bevel=0.008, rotation=(0, side * 15, 0))
    b.box((0.08, 0.5, 0.06), (-0.45, 0.05, 0.08), "tarred_wood", bevel=0.008)
    rubber_duck(b, (-0.45, 0.05, 0.13), size=1.25)
    # Belt from the axle to the saw.
    b.box((0.86, 0.03, 0.025), (-0.03, -0.12, 0.57), "tarred_wood", bevel=0.004, rotation=(0, 4, 0))
    # Saw bench under a plank roof: a log being fed into the blade.
    b.box((0.85, 0.36, 0.06), (0.45, -0.1, 0.44), "fresh_log", bevel=0.01)
    for x in (0.08, 0.82):
        for y in (-0.24, 0.04):
            b.box((0.05, 0.05, 0.42), (x, y, 0.21), "old_wood", bevel=0.006)
    b.cylinder(0.2, 0.012, (0.4, -0.12, 0.52), "mail", segments=16, rotation=(90, 0, 0))                # circular saw
    b.cylinder(0.04, 0.03, (0.4, -0.12, 0.52), "tarred_wood", segments=6, rotation=(90, 0, 0))
    b.cylinder(0.09, 0.55, (0.62, -0.1, 0.56), "fresh_log", segments=8, rotation=(0, 90, 0))
    for x in (0.05, 0.85):
        for y in (-0.36, 0.2):
            b.box((0.06, 0.06, 1.0), (x, y, 0.5), "old_wood", bevel=0.008)
    plank_roof(b, 0.95, 0.68, (0.45, -0.08, 1.02), 12)
    b.box((0.22, 0.14, 0.02), (0.3, -0.12, 1.06), "woad", bevel=0, rotation=(12, 0, 20))                # woad cloth, in the spirit of the Tarp
    # Stacked planks (the whole point).
    for k in range(7):
        b.box((0.6, 0.16, 0.035), (0.65, 0.38, 0.08 + k * 0.04), "fresh_log" if k % 2 else "thatch", bevel=0.004,
              rotation=(0, 0, (k % 3 - 1) * 3))
    # Cult pennant on a tall pole, a clay cone and a weld vest.
    b.cylinder(0.03, 1.65, (0.98, 0.42, 0.83), "old_wood", segments=6)
    b.box((0.32, 0.015, 0.18), (0.81, 0.42, 1.52), "woad", bevel=0.003, rotation=(0, 6, 0))
    rubber_duck(b, (0.98, 0.42, 1.65), size=0.6)
    clay_cone(b, (-0.95, -0.42, 0.06))
    weld_vest(b, (0.05, -0.39, 0.7))
    b.sphere(0.18, (0.4, -0.3, 0.06), "fresh_log", segments=8, rings=5, scale=(1.5, 0.8, 0.3))           # sawdust
    b.build()
    return mill
