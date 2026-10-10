"""A low cluster of field stones. Faceted on purpose: rocks are medieval, and medieval things are wonky."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    rock = root("rock_a")
    (Part("Prop", parent=rock)
     .sphere(0.32, (0, 0, 0.14), "fieldstone", segments=7, rings=5, scale=(1.2, 1.0, 0.6), rotation=(0, 0, 20))
     .sphere(0.2, (0.3, 0.12, 0.08), "worn_stone", segments=6, rings=4, scale=(1.1, 0.9, 0.6), rotation=(8, 0, 50))
     .sphere(0.13, (-0.28, -0.15, 0.05), "fieldstone", segments=6, rings=4, scale=(1.0, 1.2, 0.6), rotation=(0, 6, 10))
     .build())
    return rock
