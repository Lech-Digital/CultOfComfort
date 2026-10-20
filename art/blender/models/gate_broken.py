"""A smashed gate: the two posts still standing (one leaning), one door torn off and lying flat in the mud, the
other hanging by a hinge, splinters everywhere."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 700
REQUIRED_PARTS = ("Prop",)


def build():
    gate = root("gate_broken")
    p = Part("Prop", parent=gate)
    p.box((0.12, 0.14, 1.15), (-0.47, 0, 0.575), "old_wood", bevel=0.015)
    p.box((0.12, 0.14, 1.0), (0.5, 0.04, 0.48), "old_wood", bevel=0.015, rotation=(-6, 12, 0))             # leaning post
    p.box((0.4, 0.07, 0.85), (-0.2, -0.45, 0.04), "fresh_log", bevel=0.01, rotation=(90, 0, 14))           # door, flat
    p.box((0.42, 0.08, 0.06), (-0.2, -0.36, 0.08), "tarred_wood", bevel=0.005, rotation=(0, 0, 14))
    p.box((0.4, 0.07, 0.7), (0.26, 0.05, 0.42), "fresh_log", bevel=0.01, rotation=(0, 28, -8))             # door, hanging
    for k, (x, y, yaw) in enumerate(((0.0, 0.3, 30), (0.15, -0.2, -50), (-0.35, 0.25, 80), (0.3, -0.4, 10))):
        p.box((0.22, 0.04, 0.03), (x, y, 0.02 + k * 0.01), "fresh_log", bevel=0.0, rotation=(0, 0, yaw))   # splinters
    p.build()
    return gate
