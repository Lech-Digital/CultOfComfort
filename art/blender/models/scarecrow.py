"""A scarecrow wearing a towel turban: the cult's first convert, technically."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    crow = root("scarecrow")
    (Part("Prop", parent=crow)
     .cylinder(0.025, 1.2, (0, 0, 0.6), "old_wood", segments=5)
     .cylinder(0.02, 0.8, (0, 0, 0.92), "old_wood", segments=5, rotation=(0, 90, 0))
     .box((0.3, 0.16, 0.36), (0, 0, 0.8), "levy_wool", bevel=0.03, rotation=(0, 0, 4))                         # tunic
     .sphere(0.12, (0, 0, 1.18), "daub", segments=8, rings=6, smooth=True)                                     # sack head
     .sphere(0.13, (0, 0.01, 1.27), "linen", segments=8, rings=5, scale=(1.05, 1.0, 0.6), smooth=True)        # towel turban
     .sphere(0.02, (0.04, -0.11, 1.2), "umbrella_black", segments=5, rings=3)
     .sphere(0.02, (-0.04, -0.11, 1.2), "umbrella_black", segments=5, rings=3)
     .sphere(0.05, (0.38, 0, 0.92), "thatch", segments=6, rings=4, smooth=True)                                # straw hands
     .sphere(0.05, (-0.38, 0, 0.92), "thatch", segments=6, rings=4, smooth=True)
     .build())
    return crow
