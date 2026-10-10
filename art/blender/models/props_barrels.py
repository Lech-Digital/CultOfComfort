"""A cluster of barrels and sacks for dressing the village."""
from lib.buildings import barrel, sack
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    cluster = root("props_barrels")
    p = Part("Prop", parent=cluster)
    barrel(p, (0, 0, 0))
    barrel(p, (0.26, 0.05, 0), radius=0.11, height=0.27)
    sack(p, (-0.2, -0.15, 0))
    sack(p, (0.12, -0.24, 0), swatch="levy_wool")
    p.build()
    return cluster
