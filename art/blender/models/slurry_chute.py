"""Slurry Chute (tech tree: Mud era): a wattle hopper of liquid mud on four stilts, dumped down a plank ramp
by gravity. `Gate` is the flap that opens when it fires."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Base", "Gate")


def build():
    tower = root("slurry_chute")
    base = Part("Base", parent=tower)
    for i, (x, y) in enumerate(((-0.25, -0.22), (0.25, -0.22), (-0.25, 0.22), (0.25, 0.22))):
        base.box((0.07, 0.07, 0.75), (x, y, 0.375), "old_wood", bevel=0.01, rotation=(i % 2 * 3 - 1.5, (i // 2) * 3 - 1.5, 0))
    base.box((0.6, 0.5, 0.05), (0, 0, 0.42), "tarred_wood", bevel=0.01)                     # cross brace deck
    # Wattle hopper: woven sides (alternating bands), mud slopping over the top.
    for k in range(4):
        z = 0.82 + k * 0.09
        base.box((0.72 - k * 0.02, 0.62 - k * 0.02, 0.07), (0, 0, z), "daub" if k % 2 else "fresh_log", bevel=0.015)
    base.box((0.64, 0.54, 0.04), (0, 0, 1.13), "wet_mud", bevel=0.02, bevel_segments=2)     # the slurry
    base.sphere(0.07, (0.18, -0.2, 1.15), "wet_mud", segments=6, rings=4, scale=(1, 1, 0.5), smooth=True)  # glop
    # Plank ramp from the hopper's front lip (−Y) down to the ground; positive X tilt drops the front end.
    for k, x in enumerate((-0.14, 0.0, 0.14)):
        base.box((0.13, 0.92, 0.03), (x, -0.58, 0.47), "fresh_log", bevel=0.006, rotation=(54, 0, k - 1))
    base.box((0.42, 0.08, 0.06), (0, -0.86, 0.04), "old_wood", bevel=0.01)                  # ramp foot
    base.sphere(0.07, (0.04, -0.92, 0.04), "wet_mud", segments=6, rings=4, scale=(1.4, 1.2, 0.4), smooth=True)  # puddle
    base_obj = base.build()

    (Part("Gate", pivot=(0, -0.32, 0.86), parent=base_obj)
     .box((0.42, 0.04, 0.24), (0, -0.33, 0.96), "tarred_wood", bevel=0.01)                  # hinged flap
     .box((0.06, 0.06, 0.06), (0.17, -0.36, 1.06), "mail", bevel=0.005)                       # iron latch
     .build())
    return tower
