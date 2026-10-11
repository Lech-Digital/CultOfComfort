"""A Marchland pig: pink, round, curly tail, deeply content. The cult's most sincere member."""
from lib.animals import QUADRUPED_PARTS, Beast, build_quadruped

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = QUADRUPED_PARTS

LOOK = Beast(body=(0.38, 0.56, 0.32), leg=0.1, leg_r=0.045, coat="prophet_skin", hooves="old_wood",
             head_r=0.14, head_up=-0.02, snout=(0.065, 0.06), snout_colour="local_skin", nose="wet_mud",
             ears="flop", tail="curly")


def build():
    return build_quadruped("pig", LOOK)
