"""A tall boulder with a mossy top and pebbles. Good for blocking roads and stubbing toes."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    rock = root("rock_b")
    (Part("Prop", parent=rock)
     .sphere(0.3, (0, 0, 0.3), "worn_stone", segments=7, rings=5, scale=(1.0, 0.9, 1.15), rotation=(6, -4, 30))
     .sphere(0.29, (0.02, 0.0, 0.6), "foliage", segments=8, rings=5, scale=(1.05, 0.95, 0.3), rotation=(6, -4, 30), smooth=True)  # moss cap
     .sphere(0.09, (0.3, -0.18, 0.04), "fieldstone", segments=5, rings=4, scale=(1.2, 1.0, 0.6))
     .sphere(0.07, (-0.27, 0.2, 0.03), "fieldstone", segments=5, rings=4, scale=(1.0, 1.3, 0.6))
     .build())
    return rock
