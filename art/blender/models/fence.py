"""A wattle hurdle fence segment, one tile long."""
from lib.buildings import wattle_fence
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    fence = root("fence")
    p = Part("Prop", parent=fence)
    wattle_fence(p, (-0.5, 0), (0.5, 0))
    p.build()
    return fence
