"""Relic: the warning triangle from the boot (legally required, 2024). The cult reads it as the Holy Sign of
Caution and paints it on every gate."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    relic = root("relic_warning_triangle")
    p = Part("Prop", parent=relic)
    h = 0.26
    for k in range(3):                       # three bars of a triangle standing in the XZ plane, facing the camera
        a = math.radians(90 + k * 120)
        a2 = math.radians(90 + (k + 1) * 120)
        x0, z0 = math.cos(a) * h * 0.6, math.sin(a) * h * 0.6 + h * 0.45
        x1, z1 = math.cos(a2) * h * 0.6, math.sin(a2) * h * 0.6 + h * 0.45
        length = math.hypot(x1 - x0, z1 - z0)
        p.box((length, 0.015, 0.035), ((x0 + x1) / 2, 0, (z0 + z1) / 2), "laser_red", bevel=0.004,
              rotation=(0, -math.degrees(math.atan2(z1 - z0, x1 - x0)), 0))
    p.box((0.2, 0.06, 0.012), (0, 0.03, 0.006), "chrome", bevel=0.003)
    p.box((0.012, 0.1, 0.012), (0, 0.06, 0.04), "chrome", bevel=0.003, rotation=(-40, 0, 0))
    p.build()
    return relic
