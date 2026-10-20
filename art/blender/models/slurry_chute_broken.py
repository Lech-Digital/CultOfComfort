"""A wrecked Slurry Chute: the hopper knocked off its stilts and split open on the ground, mud everywhere, two stilts
snapped, the ramp in pieces."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1500
REQUIRED_PARTS = ("Prop",)


def build():
    tower = root("slurry_chute_broken")
    p = Part("Prop", parent=tower)
    for x, y, h, tilt in ((-0.25, -0.22, 0.75, 4), (0.25, 0.22, 0.75, -6), (0.25, -0.22, 0.3, 20), (-0.25, 0.22, 0.25, -25)):
        p.box((0.07, 0.07, h), (x, y, h / 2), "old_wood", bevel=0.01, rotation=(tilt, tilt / 2, 0))
    for k in range(3):
        p.box((0.7 - k * 0.04, 0.6, 0.07), (0.12, 0.05, 0.12 + k * 0.08), "daub" if k % 2 else "fresh_log", bevel=0.015, rotation=(28, 0, 15))
    p.sphere(0.4, (0.15, -0.1, 0.02), "wet_mud", segments=8, rings=4, scale=(1.4, 1.1, 0.12), smooth=True)  # the spill
    for k, x in enumerate((-0.2, 0.05, 0.3)):
        p.box((0.13, 0.4, 0.03), (x, -0.62 - k * 0.05, 0.03), "fresh_log", bevel=0.006, rotation=(0, 0, 20 * k - 15))
    p.build()
    return tower
