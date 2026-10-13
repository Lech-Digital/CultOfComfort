"""Relic: a magnifying glass (for reading the small print on a time-machine manual that did not exist). In the
Marchlands it catches the sun and lights things. Sun-fire. Do not point it at the Prophet."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400
REQUIRED_PARTS = ("Prop",)


def build():
    relic = root("relic_magnifier")
    (Part("Prop", parent=relic)
     .cylinder(0.09, 0.02, (0, -0.06, 0.012), "chrome", segments=16)
     .cylinder(0.078, 0.022, (0, -0.06, 0.012), "glass", segments=16)
     .box((0.03, 0.17, 0.02), (0, 0.1, 0.012), "umbrella_black", bevel=0.008)
     .build())
    return relic
