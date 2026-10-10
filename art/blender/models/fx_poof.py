"""Effect: the cloud an enemy vanishes in when defeated (they go home, sulking)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    fx = root("fx_poof")
    part = Part("Prop", parent=fx)
    for x, y, z, r in ((0, 0, 0.2, 0.16), (0.14, 0.05, 0.14, 0.11), (-0.13, -0.04, 0.13, 0.1), (0.03, 0.1, 0.32, 0.09)):
        part.sphere(r, (x, y, z), "linen", segments=7, rings=5, smooth=True)
    part.build()
    return fx
