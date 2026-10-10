"""Effect: a hymnal thrown by a Zealot, spinning, pages flapping."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    fx = root("fx_hymnal")
    (Part("Prop", parent=fx)
     .box((0.16, 0.2, 0.025), (0, 0, 0.03), "tarred_wood", bevel=0.006, rotation=(0, -12, 0))
     .box((0.16, 0.2, 0.025), (0, 0, 0.06), "tarred_wood", bevel=0.006, rotation=(0, 12, 0))
     .box((0.14, 0.18, 0.03), (0, 0, 0.045), "linen", bevel=0.003)
     .build())
    return fx
