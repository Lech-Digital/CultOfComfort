"""Rubble: what's left when the lord's men are done with a building. A heap of broken planks and fallen stones, a
snapped beam sticking up, a smashed barrel and a scorched patch. Laid under and around a wrecked building."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 900
REQUIRED_PARTS = ("Prop",)


def build():
    rubble = root("rubble")
    p = Part("Prop", parent=rubble)
    p.cylinder(0.62, 0.02, (0, 0, 0.01), "tarred_wood", segments=10)                                       # scorch mark
    for k, (x, y, yaw, tilt) in enumerate(((-0.25, 0.1, 20, 8), (0.18, -0.2, -35, -6), (0.05, 0.28, 70, 12),
                                            (-0.3, -0.25, 110, -10), (0.32, 0.18, -10, 5), (0.0, -0.05, 45, 18))):
        p.box((0.55, 0.09, 0.05), (x, y, 0.05 + k * 0.025), "old_wood" if k % 2 else "fresh_log", bevel=0.008, rotation=(tilt, 0, yaw))
    for x, y, r in ((0.3, -0.32, 0.09), (-0.38, 0.28, 0.08), (0.12, 0.38, 0.07), (-0.1, -0.38, 0.1), (0.4, 0.05, 0.06)):
        p.sphere(r, (x, y, r * 0.6), "fieldstone", segments=6, rings=4)
    p.box((0.08, 0.08, 0.6), (-0.05, 0.05, 0.28), "tarred_wood", bevel=0.01, rotation=(18, -12, 30))       # snapped beam
    p.cylinder(0.09, 0.12, (0.22, 0.32, 0.06), "old_wood", segments=7, rotation=(80, 0, 20))               # smashed barrel
    p.build()
    return rubble
