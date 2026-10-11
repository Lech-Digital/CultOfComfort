"""A torch on a wonky pole: the Night's light pools (spec: Golden Mud lighting)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop", "Flame")


def build():
    torch = root("torch")
    pole = (Part("Prop", parent=torch)
            .cylinder(0.03, 1.1, (0, 0, 0.55), "old_wood", segments=6, rotation=(3, -2, 0))
            .cylinder(0.055, 0.14, (0.01, -0.01, 1.08), "tarred_wood", segments=7)         # pitch-soaked rag
            .build())
    (Part("Flame", pivot=(0.01, -0.01, 1.15), parent=pole)
     .cylinder(0.1, 0.32, (0.01, -0.01, 1.3), "fire", segments=7, radius_top=0.0, smooth=True)
     .sphere(0.08, (0.01, -0.01, 1.2), "ember", segments=7, rings=5, smooth=True)
     .build())
    return torch
