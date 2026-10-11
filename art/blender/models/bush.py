"""A berry bush: three soft clumps and a few red berries (do not eat; ask the zealots why)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    bush = root("bush")
    part = (Part("Prop", parent=bush)
            .sphere(0.28, (0, 0, 0.2), "foliage", segments=8, rings=6, scale=(1.1, 1.0, 0.8), smooth=True)
            .sphere(0.2, (0.25, 0.08, 0.15), "grass", segments=7, rings=5, smooth=True)
            .sphere(0.18, (-0.22, -0.1, 0.13), "foliage", segments=7, rings=5, smooth=True))
    for x, y, z in ((0.12, -0.22, 0.28), (-0.08, -0.24, 0.2), (0.3, -0.1, 0.22), (-0.25, -0.2, 0.12)):
        part.sphere(0.03, (x, y, z), "zealot_red", segments=5, rings=4, smooth=True)
    part.build()
    return bush
