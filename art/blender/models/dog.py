"""The village dog: scruffy, loyal to whoever is holding food, currently the Prophet."""
from lib.animals import QUADRUPED_PARTS, Beast, build_quadruped

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = QUADRUPED_PARTS

LOOK = Beast(body=(0.24, 0.46, 0.24), leg=0.16, leg_r=0.035, coat="old_wood", belly="daub", legs="old_wood",
             hooves="tarred_wood", head_r=0.11, head_up=0.06, snout=(0.05, 0.07), snout_colour="daub",
             nose="umbrella_black", ears="flop", ear_colour="tarred_wood", tail="long", tail_angle=-55, eye_size=1.15)


def build():
    return build_quadruped("dog", LOOK)
