"""A wooden wheelbarrow full of turnips, abandoned the moment someone mentioned hot water."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    barrow = root("wheelbarrow")
    part = (Part("Prop", parent=barrow)
            .box((0.42, 0.55, 0.2), (0, 0, 0.3), "old_wood", bevel=0.02, rotation=(-6, 0, 0))
            .cylinder(0.14, 0.05, (0, -0.38, 0.14), "fresh_log", segments=10, rotation=(0, 90, 0))
            .cylinder(0.04, 0.06, (0, -0.38, 0.14), "tarred_wood", segments=6, rotation=(0, 90, 0)))
    for x in (-0.16, 0.16):
        part.box((0.04, 0.6, 0.04), (x, 0.38, 0.32), "fresh_log", bevel=0.008, rotation=(-12, 0, 0))            # handles
        part.box((0.04, 0.04, 0.2), (x, 0.18, 0.1), "old_wood", bevel=0.006)                                   # legs
    for x, y in ((-0.08, -0.05), (0.08, 0.06), (0.0, 0.15), (0.1, -0.12)):
        part.sphere(0.06, (x, y, 0.42), "roman_marble", segments=7, rings=5, smooth=True)                      # turnips
        part.cylinder(0.01, 0.06, (x, y, 0.49), "foliage", segments=4)
    part.build()
    return barrow
