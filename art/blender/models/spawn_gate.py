"""Where Wulfric's men enter the map: two posts, a crossbar and his red banner with a gold boar-ish blob."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop",)


def build():
    gate = root("spawn_gate")
    (Part("Prop", parent=gate)
     .box((0.1, 0.1, 1.5), (-0.45, 0, 0.75), "old_wood", bevel=0.015, rotation=(0, 2, 0))
     .box((0.1, 0.1, 1.45), (0.45, 0, 0.72), "old_wood", bevel=0.015, rotation=(0, -3, 0))
     .box((1.1, 0.08, 0.08), (0, 0, 1.42), "tarred_wood", bevel=0.01, rotation=(0, 2, 0))
     .box((0.6, 0.03, 0.75), (0, -0.04, 1.0), "wulfric_red", bevel=0.01, rotation=(4, 0, 2))  # banner
     .box((0.62, 0.035, 0.05), (0, -0.045, 0.63), "gold_trim", bevel=0.005, rotation=(4, 0, 2))  # fringe
     .sphere(0.12, (0, -0.07, 1.08), "gold_trim", segments=8, rings=5, scale=(1.3, 0.3, 0.9))   # the boar (artist's impression)
     .build())
    return gate
