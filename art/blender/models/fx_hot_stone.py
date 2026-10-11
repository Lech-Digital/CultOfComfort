"""Effect: a Hot Stone Therapy stone, glowing at the edges."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    fx = root("fx_hot_stone")
    (Part("Prop", parent=fx)
     .sphere(0.1, (0, 0, 0.1), "fieldstone", segments=7, rings=5, scale=(1.1, 0.9, 0.7))
     .sphere(0.06, (0.03, -0.02, 0.15), "ember", segments=6, rings=4, smooth=True)
     .build())
    return fx
