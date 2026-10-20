"""The Tavern ("The Comfy Duck"): a long low timber hall with a deep thatch, a painted sign of a duck in a bath, barrels
stacked by the door, a bench, and a chimney that never stops smoking."""
from lib.buildings import barrel, log_wall, thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3200
REQUIRED_PARTS = ("Building", "Sign")


def build():
    tavern = root("tavern")
    b = Part("Building", parent=tavern)
    b.box((1.8, 1.2, 0.06), (0, 0, 0.03), "worn_stone", bevel=0.02)
    corners = ((-0.75, -0.35), (0.75, -0.35), (0.75, 0.4), (-0.75, 0.4))
    for k in range(4):
        log_wall(b, corners[k], corners[(k + 1) % 4], height=0.55, z0=0.06, radius=0.05)
    thatch_roof(b, 1.55, 0.8, (0, 0.025, 0.64), 0.34, overhang=0.08, lean=1.4)
    b.box((0.18, 0.18, 0.5), (0.5, 0.2, 1.05), "fieldstone", bevel=0.015)                                       # chimney
    b.box((0.26, 0.04, 0.42), (-0.1, -0.37, 0.27), "tarred_wood", bevel=0.01)                                  # door
    for x in (-0.5, 0.3):
        b.box((0.2, 0.03, 0.16), (x, -0.37, 0.38), "window", bevel=0.005)
    for x, y in ((0.62, -0.55), (0.82, -0.5), (0.72, -0.62)):
        barrel(b, (x, y, 0.06), radius=0.1, height=0.25)
    b.box((0.55, 0.14, 0.04), (-0.45, -0.6, 0.2), "fresh_log", bevel=0.01)                                     # bench
    for x in (-0.68, -0.22):
        b.box((0.04, 0.12, 0.18), (x, -0.6, 0.09), "old_wood", bevel=0.005)
    b.cylinder(0.02, 0.5, (0.12, -0.45, 0.95), "old_wood", segments=4, rotation=(0, 90, 0))                    # sign arm
    building = b.build()
    (Part("Sign", pivot=(0.25, -0.45, 0.95), parent=building)
     .box((0.26, 0.03, 0.2), (0.3, -0.45, 0.8), "old_wood", bevel=0.005)
     .sphere(0.05, (0.3, -0.47, 0.82), "weld", segments=6, rings=4)                                            # the duck
     .build())
    return tavern
