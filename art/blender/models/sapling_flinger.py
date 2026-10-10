"""Sapling Flinger (tech tree: Mud era): a young tree lashed down and bent back, a sling cup with a rock,
a wonky frame and a pile of ammunition. Medieval, so nothing is quite straight. `Arm` is the part that flings."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Base", "Arm")


def build():
    tower = root("sapling_flinger")
    base = (Part("Base", parent=tower)
            .box((0.8, 0.5, 0.14), (0, 0, 0.07), "old_wood", bevel=0.02, rotation=(0, 0, 3))         # sledge
            .box((0.08, 0.08, 0.7), (0.3, -0.18, 0.45), "old_wood", bevel=0.012, rotation=(3, -6, 0))  # frame post
            .box((0.08, 0.08, 0.7), (0.3, 0.18, 0.45), "old_wood", bevel=0.012, rotation=(-3, -4, 0))
            .box((0.08, 0.5, 0.08), (0.28, 0, 0.78), "tarred_wood", bevel=0.012)                     # crossbar the arm hits
            .cylinder(0.035, 0.14, (0.28, 0, 0.84), "thatch", segments=6, rotation=(90, 0, 0))       # rope padding
            .cylinder(0.11, 0.18, (-0.3, 0, 0.23), "fresh_log", segments=8)                           # root ball of the sapling
            .cylinder(0.02, 0.5, (-0.05, 0.0, 0.32), "thatch", segments=5, rotation=(0, 70, 0)))     # lashing rope
    for x, y, r in ((0.12, -0.3, 0.07), (0.2, -0.36, 0.06), (0.05, -0.38, 0.055), (0.16, -0.25, 0.05)):
        base.sphere(r, (x, y, 0.14 + r * 0.7), "fieldstone", segments=6, rings=4)                    # ammunition
    base_obj = base.build()

    # The sapling: trunk bent back over the frame, leafy tuft, sling cup with a rock at the tip.
    (Part("Arm", pivot=(-0.3, 0, 0.3), parent=base_obj)
     .cylinder(0.05, 0.6, (-0.22, 0, 0.6), "fresh_log", segments=7, radius_top=0.035, rotation=(0, 15, 0))
     .cylinder(0.035, 0.5, (-0.02, 0, 1.0), "fresh_log", segments=6, radius_top=0.02, rotation=(0, 45, 0))
     .sphere(0.13, (-0.2, 0.05, 0.95), "foliage", segments=7, rings=5, smooth=True)                 # stubborn leaves
     .sphere(0.09, (-0.1, -0.06, 1.05), "grass", segments=6, rings=5, smooth=True)
     .cylinder(0.09, 0.06, (0.18, 0, 1.16), "levy_wool", segments=8, radius_top=0.11)                 # sling cup
     .sphere(0.075, (0.18, 0, 1.22), "worn_stone", segments=6, rings=4)                                # the rock
     .build())
    return tower
