"""Iron-bound Gate: heavier doors strapped with iron, studs, and a ring to knock with (nobody knocks)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1200
REQUIRED_PARTS = ("Prop",)


def build():
    gate = root("gate_iron")
    p = Part("Prop", parent=gate)
    for x in (-0.48, 0.48):
        p.box((0.16, 0.18, 1.3), (x, 0, 0.65), "tarred_wood", bevel=0.02)
        p.box((0.2, 0.22, 0.08), (x, 0, 1.33), "mail", bevel=0.01)
    for side in (-1, 1):
        x = side * 0.2
        p.box((0.4, 0.1, 1.0), (x, 0, 0.55), "old_wood", bevel=0.01)
        for z in (0.22, 0.55, 0.88):
            p.box((0.42, 0.11, 0.07), (x, -0.01, z), "mail", bevel=0.008)                    # iron straps
            for k in (-1, 1):
                p.sphere(0.022, (x + k * 0.13, -0.065, z), "umbrella_black", segments=5, rings=3)  # studs
    p.cylinder(0.05, 0.015, (0.08, -0.07, 0.6), "gold_trim", segments=8, rotation=(90, 0, 0))  # knocker ring
    p.build()
    return gate
