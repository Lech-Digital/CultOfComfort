"""A straw beehive skep on a stump. Honey goes in the sauna, apparently."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    hive = root("beehive")
    part = (Part("Prop", parent=hive)
            .cylinder(0.16, 0.3, (0, 0, 0.15), "old_wood", segments=8)
            .sphere(0.17, (0, 0, 0.38), "thatch", segments=10, rings=7, scale=(1, 1, 1.2), smooth=True))
    for z in (0.32, 0.4, 0.48):
        part.cylinder(0.17 - (z - 0.32) * 0.5, 0.02, (0, 0, z), "fresh_log", segments=10)
    part.box((0.05, 0.02, 0.03), (0, -0.16, 0.33), "tarred_wood", bevel=0.004)                                 # entrance
    part.build()
    return hive
