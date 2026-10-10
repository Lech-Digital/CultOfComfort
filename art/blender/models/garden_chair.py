"""A white plastic garden chair (2024). The Prophet's throne. It wobbles."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    chair = root("garden_chair")
    part = (Part("Prop", parent=chair)
            .box((0.42, 0.4, 0.04), (0, 0, 0.4), "linen", bevel=0.015)
            .box((0.42, 0.04, 0.38), (0, 0.2, 0.62), "linen", bevel=0.015, rotation=(-8, 0, 0)))
    for x in (-0.18, 0.18):
        for y in (-0.17, 0.17):
            part.cylinder(0.025, 0.4, (x, y, 0.2), "linen", segments=6, rotation=(y * 30, -x * 30, 0))
    for x in (-0.2, 0.2):
        part.box((0.04, 0.36, 0.04), (x, 0.0, 0.58), "linen", bevel=0.012)                                     # armrests
    part.build()
    return chair
