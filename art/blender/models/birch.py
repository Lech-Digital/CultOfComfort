"""A slim birch: pale trunk with dark bands, a narrow airy canopy."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    tree = root("birch")
    part = (Part("Prop", parent=tree)
            .cylinder(0.06, 1.3, (0, 0, 0.65), "roman_marble", segments=7, rotation=(2, -3, 0)))
    for z, w in ((0.3, 0.04), (0.62, 0.03), (0.9, 0.035), (1.15, 0.025)):
        part.box((0.1, 0.1, w), (0, 0, z), "tarred_wood", bevel=0, rotation=(2, -3, z * 40))
    (part.sphere(0.3, (0.02, 0, 1.35), "foliage", segments=8, rings=6, scale=(0.8, 0.8, 1.3), smooth=True)
     .sphere(0.22, (0.15, 0.05, 1.1), "grass", segments=7, rings=5, scale=(0.8, 0.8, 1.2), smooth=True)
     .build())
    return tree
