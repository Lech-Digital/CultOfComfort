"""Speed: a snail, unhurried."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_speed")
    part = Part("Prop", parent=icon)
    part.sphere(0.06, (0, 0, 0.05), "daub", segments=8, rings=5, scale=(4.0, 1.0, 0.8), smooth=True)
    part.sphere(0.13, (0.05, 0, 0.18), "levy_wool", segments=9, rings=6, smooth=True)
    part.sphere(0.07, (0.07, -0.08, 0.19), "thatch", segments=8, rings=5, smooth=True)
    for x in (-0.2, -0.16):
        part.cylinder(0.008, 0.1, (x, 0, 0.13), "daub", segments=4, rotation=(0, -15, 0))
        part.sphere(0.015, (x - 0.01, 0, 0.18), "umbrella_black", segments=5, rings=3)
    part.build()
    return icon
