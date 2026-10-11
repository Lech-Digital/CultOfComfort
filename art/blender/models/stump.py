"""A tree stump with an axe left in it. Someone was clearly interrupted by a better idea (a bath)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    stump = root("stump")
    (Part("Prop", parent=stump)
     .cylinder(0.22, 0.32, (0, 0, 0.16), "old_wood", segments=9, radius_top=0.2)
     .cylinder(0.19, 0.02, (0, 0, 0.325), "fresh_log", segments=9)                            # cut top
     .cylinder(0.08, 0.025, (0, 0, 0.335), "thatch", segments=8)                              # growth rings
     .cylinder(0.02, 0.4, (0.08, -0.05, 0.47), "fresh_log", segments=5, rotation=(0, 35, 0))  # axe handle
     .box((0.14, 0.02, 0.09), (0.0, -0.05, 0.36), "mail", bevel=0.004, rotation=(0, 35, 0))   # axe head, buried
     .build())
    return stump
