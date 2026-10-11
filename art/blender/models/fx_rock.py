"""Effect: the rock a Sapling Flinger throws."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    fx = root("fx_rock")
    Part("Prop", parent=fx).sphere(0.09, (0, 0, 0.09), "worn_stone", segments=6, rings=4, scale=(1.1, 0.95, 0.85), rotation=(10, 20, 30)).build()
    return fx
