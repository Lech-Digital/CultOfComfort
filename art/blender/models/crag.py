"""Bedrock: bare, sharp, grey shards of the mountain poking through the mud. No moss: it can't be moved and looks it."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400
REQUIRED_PARTS = ("Prop",)


def build():
    crag = root("crag")
    (Part("Prop", parent=crag)
     .cylinder(0.36, 0.95, (0, 0, 0.47), "fieldstone", segments=5, radius_top=0.1, rotation=(4, -3, 12))
     .cylinder(0.24, 0.62, (0.27, 0.16, 0.3), "worn_stone", segments=5, radius_top=0.06, rotation=(-8, 10, 50))
     .cylinder(0.2, 0.48, (-0.26, -0.18, 0.23), "fieldstone", segments=4, radius_top=0.05, rotation=(9, -6, 30))
     .build())
    return crag
