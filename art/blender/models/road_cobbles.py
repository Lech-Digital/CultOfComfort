"""A cobbled road tile: a grid of rounded stones in sand. Roman, nearly. The Bishop approves."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    tile = root("road_cobbles")
    part = Part("Prop", parent=tile).box((0.98, 0.98, 0.03), (0, 0, 0.015), "daub", bevel=0.0)
    for row in range(3):
        for col in range(3):
            swatch = "worn_stone" if (row + col) % 2 == 0 else "fieldstone"
            part.box((0.28, 0.28, 0.06), (-0.31 + col * 0.31, -0.31 + row * 0.31, 0.05), swatch, bevel=0.03, bevel_segments=1)
    part.build()
    return tile
