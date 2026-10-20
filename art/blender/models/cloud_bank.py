"""A bank of cloud: a few lumpy low-poly puffs, white with a grey underside. A ring of them hides the edge of the world
when the camera pulls right back."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 500
REQUIRED_PARTS = ("Prop",)

PUFFS = ((0, 0, 1.0, 1.0), (1.2, 0.3, 0.8, 0.8), (-1.1, -0.2, 0.75, 0.85), (0.4, -0.9, 0.7, 0.7), (-0.3, 0.9, 0.65, 0.75))


def build():
    cloud = root("cloud_bank")
    p = Part("Prop", parent=cloud)
    for x, y, r, h in PUFFS:
        p.sphere(r, (x, y, h * 0.9), "linen", segments=7, rings=4, scale=(1.0, 1.0, 0.6))
        p.sphere(r * 0.9, (x, y, h * 0.55), "worn_stone", segments=6, rings=3, scale=(1.0, 1.0, 0.35))  # the grey underside
    p.build()
    return cloud
