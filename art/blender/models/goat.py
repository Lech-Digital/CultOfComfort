"""A goat: horns, a beard, and opinions. Eats tarps."""
from lib.animals import QUADRUPED_PARTS, Beast, build_quadruped

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = QUADRUPED_PARTS


def _horns_and_beard(parts, b):
    w, l, h = b.body
    head_y = -l / 2 - b.head_r * 0.3
    head_z = b.leg + h + b.head_up
    for side in (-1, 1):
        parts["Head"].cylinder(0.025, 0.16, (side * 0.05, head_y + 0.04, head_z + 0.12), "worn_stone", segments=6,
                               radius_top=0.004, rotation=(-35, side * 12, 0))
    parts["Head"].cylinder(0.03, 0.09, (0, head_y - 0.11, head_z - 0.12), "daub", segments=6, radius_top=0.005,
                           rotation=(170, 0, 0), smooth=True)  # beard


LOOK = Beast(body=(0.3, 0.52, 0.28), leg=0.2, leg_r=0.03, coat="daub", belly="linen", legs="daub", hooves="tarred_wood",
             head_r=0.11, head_up=0.1, snout=(0.05, 0.06), nose="tarred_wood", ears="point", tail="short",
             extras=[_horns_and_beard])


def build():
    return build_quadruped("goat", LOOK)
