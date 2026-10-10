"""A hay cart with two big wheels, piled high. Wulfric's men hide behind these; so does everyone else."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    cart = root("haycart")
    part = (Part("Building", parent=cart)
            .box((0.75, 1.3, 0.08), (0, 0, 0.42), "old_wood", bevel=0.015)
            .box((0.06, 1.3, 0.2), (-0.36, 0, 0.55), "fresh_log", bevel=0.01)
            .box((0.06, 1.3, 0.2), (0.36, 0, 0.55), "fresh_log", bevel=0.01))
    for x in (-0.42, 0.42):
        part.cylinder(0.32, 0.06, (x, 0.1, 0.32), "old_wood", segments=12, rotation=(0, 90, 0))
        part.cylinder(0.06, 0.08, (x, 0.1, 0.32), "tarred_wood", segments=6, rotation=(0, 90, 0))
    part.sphere(0.5, (0, 0, 0.7), "thatch", segments=10, rings=6, scale=(0.75, 1.25, 0.6), smooth=True)        # the hay
    part.sphere(0.25, (0.1, -0.2, 0.98), "thatch", segments=8, rings=5, smooth=True)
    part.box((0.06, 0.9, 0.06), (-0.12, -1.05, 0.38), "fresh_log", bevel=0.008, rotation=(6, 0, 4))           # shafts
    part.box((0.06, 0.9, 0.06), (0.12, -1.05, 0.38), "fresh_log", bevel=0.008, rotation=(6, 0, -4))
    part.cylinder(0.02, 0.6, (0.2, 0.35, 1.05), "fresh_log", segments=4, rotation=(30, 20, 0))                # pitchfork in the hay
    part.build()
    return cart
