"""What a builder works from: a plank on two stones, a bundle of reeds, a bucket of mud and a few sticks. Sits by
anything under construction."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    pile = root("build_pile")
    p = Part("Prop", parent=pile)
    for x in (-0.16, 0.16):
        p.sphere(0.06, (x, 0, 0.04), "fieldstone", segments=6, rings=4)
    p.box((0.46, 0.1, 0.025), (0, 0, 0.1), "fresh_log", bevel=0.005, rotation=(0, 0, 3))               # the plank
    p.box((0.4, 0.09, 0.025), (0.02, 0.01, 0.125), "old_wood", bevel=0.005, rotation=(0, 0, -6))
    for k in range(5):                                                                                 # reeds, tied
        p.cylinder(0.012, 0.42, (-0.06 + k * 0.012, 0.16, 0.03 + (k % 2) * 0.012), "thatch", segments=4, rotation=(0, 90, 8))
    p.cylinder(0.022, 0.02, (-0.06, 0.16, 0.03), "woad", segments=6, rotation=(0, 90, 8))
    p.cylinder(0.08, 0.13, (0.24, -0.14, 0.065), "old_wood", segments=8, radius_top=0.09)              # the mud bucket
    p.cylinder(0.075, 0.01, (0.24, -0.14, 0.125), "wet_mud", segments=8)
    for k, (x, y, yaw) in enumerate(((-0.2, -0.16, 20), (-0.12, -0.2, -15), (-0.25, -0.1, 70))):        # sticks
        p.cylinder(0.01, 0.3, (x, y, 0.012 + k * 0.012), "old_wood", segments=4, rotation=(0, 90, yaw))
    p.build()
    return pile
