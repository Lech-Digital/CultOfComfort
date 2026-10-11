"""Settings: an iron cog."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_settings")
    part = Part("Prop", parent=icon)
    part.cylinder(0.16, 0.06, (0, 0, 0.2), "mail", segments=16, rotation=(90, 0, 0))
    part.cylinder(0.05, 0.065, (0, 0, 0.2), "umbrella_black", segments=10, rotation=(90, 0, 0))
    for k in range(8):
        a = math.radians(k * 45)
        part.box((0.07, 0.06, 0.06), (math.cos(a) * 0.18, 0, 0.2 + math.sin(a) * 0.18), "mail", bevel=0,
                 rotation=(0, -k * 45, 0))
    part.build()
    return icon
