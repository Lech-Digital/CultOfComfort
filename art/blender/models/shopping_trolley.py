"""A shopping trolley (2024), now the village's finest vehicle. One wheel has opinions."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    trolley = root("shopping_trolley")
    part = Part("Prop", parent=trolley)
    for z in (0.45, 0.62, 0.8):
        part.box((0.5, 0.78, 0.02), (0, 0, z), "mail", bevel=0.004)                                            # wire rings
    part.box((0.48, 0.76, 0.35), (0, 0, 0.63), "mail", bevel=0.004)                                            # (see-through-ish body)
    part.box((0.52, 0.04, 0.04), (0, 0.42, 0.95), "cone_orange", bevel=0.01)                                   # handle
    for x in (-0.22, 0.22):
        part.box((0.03, 0.03, 0.5), (x, 0.4, 0.7), "mail", bevel=0.004)
        for y in (-0.34, 0.34):
            part.box((0.03, 0.03, 0.35), (x, y, 0.27), "mail", bevel=0.004)
            part.cylinder(0.045, 0.03, (x, y, 0.05), "umbrella_black", segments=8, rotation=(0, 90, 0))
    part.build()
    return trolley
