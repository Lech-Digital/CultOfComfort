"""Woodcutter, level 2 — the Saw Pit: a proper daub shed (with a door that shuts, mostly), a big log on trestles
with a two-man saw stuck in it, a sawdust heap, a hi-vis vest on a post and a traffic cone. Safety first, second
and occasionally third."""
from lib.buildings import thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 4000
REQUIRED_PARTS = ("Building",)


def build():
    yard = root("woodcutter_l2")
    b = Part("Building", parent=yard)
    b.box((1.9, 1.0, 0.06), (0, 0, 0.03), "road_mud", bevel=0.02)                                      # trodden yard
    # The shed.
    b.box((0.68, 0.52, 0.55), (-0.5, 0.12, 0.33), "daub", bevel=0.03, rotation=(0, 0, -2))
    for x in (-0.84, -0.16):
        b.box((0.06, 0.54, 0.57), (x, 0.12, 0.33), "old_wood", bevel=0.01, rotation=(0, 0, -2))
    b.box((0.7, 0.06, 0.05), (-0.5, -0.15, 0.58), "old_wood", bevel=0.01)
    thatch_roof(b, 0.68, 0.52, (-0.5, 0.12, 0.6), 0.45, lean=-2)
    b.box((0.2, 0.04, 0.36), (-0.56, -0.15, 0.24), "tarred_wood", bevel=0.01, rotation=(0, 0, 9))      # door, ajar
    b.box((0.12, 0.03, 0.1), (-0.3, -0.15, 0.42), "window", bevel=0.005)
    for k in range(3):                                                                                  # logs against the shed
        b.cylinder(0.06, 0.55, (-0.95 + 0.13 * k, -0.3, 0.27), "fresh_log", segments=7, rotation=(0, 12 - k * 4, 0))
    # Log pile behind the saw.
    for k in range(4):
        for j in range(4 - k):
            b.cylinder(0.075, 0.8, (0.45 + (j - (3 - k) / 2) * 0.16, 0.32, 0.11 + k * 0.13), "fresh_log", segments=7,
                       rotation=(0, 90, 0))
    # Trestles with a log on them and a crosscut saw halfway through.
    for x in (0.18, 0.78):
        for side in (-1, 1):
            b.box((0.05, 0.05, 0.5), (x + side * 0.08, -0.15, 0.24), "old_wood", bevel=0.006, rotation=(0, side * 18, 0))
    b.cylinder(0.12, 0.9, (0.48, -0.15, 0.52), "fresh_log", segments=9, rotation=(0, 90, 0))
    b.cylinder(0.12, 0.02, (0.93, -0.15, 0.52), "thatch", segments=9, rotation=(0, 90, 0))              # cut face
    b.box((0.012, 0.62, 0.09), (0.55, -0.15, 0.6), "mail", bevel=0.003)                                  # saw blade
    for y in (-0.47, 0.17):
        b.cylinder(0.022, 0.2, (0.55, y, 0.62), "fresh_log", segments=5)
    b.sphere(0.2, (0.52, -0.12, 0.06), "fresh_log", segments=8, rings=5, scale=(1.4, 1.0, 0.3))          # sawdust
    # Hi-vis vest on a post, and the cone.
    b.cylinder(0.025, 0.7, (0.86, -0.42, 0.35), "old_wood", segments=5)
    b.box((0.16, 0.04, 0.2), (0.86, -0.45, 0.55), "hivis_yellow", bevel=0.01)
    b.box((0.17, 0.042, 0.025), (0.86, -0.45, 0.52), "linen", bevel=0.003)
    b.box((0.13, 0.13, 0.02), (-0.08, -0.4, 0.07), "cone_orange", bevel=0.004)
    b.cylinder(0.06, 0.22, (-0.08, -0.4, 0.18), "cone_orange", segments=8, radius_top=0.012)
    b.cylinder(0.042, 0.03, (-0.08, -0.4, 0.17), "linen", segments=8)
    b.build()
    return yard
