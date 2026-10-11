"""A cat: grey, pointy-eared, unimpressed by the Prophet, which makes two of them."""
from lib.animals import QUADRUPED_PARTS, Beast, build_quadruped

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = QUADRUPED_PARTS

LOOK = Beast(body=(0.18, 0.36, 0.18), leg=0.12, leg_r=0.025, coat="fieldstone", belly="worn_stone", legs="fieldstone",
             hooves="fieldstone", head_r=0.09, head_up=0.05, snout=None, ears="point", tail="long", tail_angle=-20, eye_size=1.2)


def build():
    return build_quadruped("cat", LOOK)
