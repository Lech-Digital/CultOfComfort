"""Effect: a mud splat on the ground where slurry lands; muddied enemies stand in it."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    fx = root("fx_splat")
    part = Part("Prop", parent=fx).cylinder(0.45, 0.02, (0, 0, 0.01), "wet_mud", segments=10, smooth=True)
    for k, (x, y, r) in enumerate(((0.45, 0.1, 0.1), (-0.4, 0.25, 0.08), (0.1, -0.48, 0.09), (-0.2, -0.38, 0.06), (0.3, 0.38, 0.07))):
        part.cylinder(r, 0.02, (x, y, 0.01), "wet_mud" if k % 2 else "road_mud", segments=7, smooth=True)
    part.build()
    return fx
