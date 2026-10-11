"""Effect: a glob of slurry in flight (Slurry Chute shot)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    fx = root("fx_mud_glob")
    (Part("Prop", parent=fx)
     .sphere(0.12, (0, 0, 0.12), "wet_mud", segments=8, rings=6, scale=(1.1, 0.9, 0.8), smooth=True)
     .sphere(0.05, (0.08, 0.06, 0.18), "road_mud", segments=6, rings=4, smooth=True)
     .sphere(0.04, (-0.09, -0.04, 0.07), "wet_mud", segments=6, rings=4, smooth=True)
     .build())
    return fx
