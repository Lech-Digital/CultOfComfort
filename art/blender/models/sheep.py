"""A sheep: a cloud of wool on four sticks, a dark face that has never been surprised by anything."""
from lib.animals import QUADRUPED_PARTS, Beast, build_quadruped

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = QUADRUPED_PARTS


def _fleece(parts, b):
    w, l, h = b.body
    for x, y, z in ((0.1, -0.15, 0.36), (-0.1, 0.0, 0.38), (0.08, 0.15, 0.37), (-0.06, -0.2, 0.33), (0.0, 0.05, 0.42)):
        parts["Body"].sphere(0.12, (x, y, z), "linen", segments=7, rings=5, smooth=True)


LOOK = Beast(body=(0.4, 0.55, 0.32), leg=0.16, leg_r=0.03, coat="linen", legs="tarred_wood", hooves="tarred_wood",
             head_r=0.11, head_up=0.03, snout=(0.05, 0.05), snout_colour="tarred_wood", nose="umbrella_black",
             ears="flop", ear_colour="tarred_wood", tail="short", extras=[_fleece])


def build():
    return build_quadruped("sheep", LOOK)
