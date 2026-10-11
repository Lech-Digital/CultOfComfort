"""A clawfoot bathtub from 2024 that somehow came through too. Enamelled, gold feet, absurdly out of place."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    tub = root("bathtub")
    part = (Part("Prop", parent=tub)
            .box((0.55, 1.05, 0.38), (0, 0, 0.33), "roman_marble", bevel=0.14, bevel_segments=3, smooth=True)
            .box((0.43, 0.9, 0.06), (0, 0, 0.5), "water", bevel=0.08, bevel_segments=2, smooth=True))
    for x in (-0.2, 0.2):
        for y in (-0.38, 0.38):
            part.sphere(0.05, (x, y, 0.07), "gold_trim", segments=6, rings=4, scale=(1, 1, 1.3), smooth=True)  # claw feet
    part.cylinder(0.02, 0.18, (0, 0.5, 0.6), "gold_trim", segments=6, rotation=(30, 0, 0))                     # tap
    part.build()
    return tub
