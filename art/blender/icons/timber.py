"""Timber: two stacked logs with pale cut ends."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_timber")
    part = Part("Prop", parent=icon)
    for x, z in ((-0.11, 0.1), (0.11, 0.1), (0.0, 0.28)):
        part.cylinder(0.1, 0.5, (x, 0, z), "fresh_log", segments=10, rotation=(90, 0, 0))
        part.cylinder(0.085, 0.01, (x, -0.252, z), "daub", segments=10, rotation=(90, 0, 0))
    part.build()
    return icon
