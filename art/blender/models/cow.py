"""A cow: big, patchy, slow, absolutely certain the bathhouse was built for her."""
from lib.animals import QUADRUPED_PARTS, Beast, build_quadruped

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = QUADRUPED_PARTS


def _patches_and_horns(parts, b):
    w, l, h = b.body
    for x, y, z, r in ((0.18, -0.1, 0.5, 0.11), (-0.17, 0.15, 0.48, 0.13), (0.05, 0.25, 0.6, 0.09)):
        parts["Body"].sphere(r, (x, y, z), "tarred_wood", segments=7, rings=5, scale=(0.6, 1, 0.8), smooth=True)
    head_y = -l / 2 - b.head_r * 0.3
    head_z = b.leg + h + b.head_up
    for side in (-1, 1):
        parts["Head"].cylinder(0.02, 0.12, (side * 0.13, head_y + 0.02, head_z + 0.1), "roman_marble", segments=6,
                               radius_top=0.004, rotation=(0, side * 60, 0))


LOOK = Beast(body=(0.46, 0.8, 0.42), leg=0.24, leg_r=0.055, coat="linen", legs="linen", hooves="tarred_wood",
             head_r=0.15, head_up=0.0, snout=(0.09, 0.07), snout_colour="local_skin", nose="wet_mud",
             ears="flop", tail="long", tail_angle=165, extras=[_patches_and_horns])


def build():
    return build_quadruped("cow", LOOK)
