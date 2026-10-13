"""Relic: a Bluetooth speaker, 87% battery when it left 2024. The Voice from the Box: it speaks in the Prophet's
voice from across the field (the Phantom Voice). Rubber bumpers, one fabric grille, one blinking light."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400
REQUIRED_PARTS = ("Prop",)


def build():
    relic = root("relic_speaker")
    (Part("Prop", parent=relic)
     .cylinder(0.07, 0.24, (0, 0, 0.07), "umbrella_black", segments=12, rotation=(0, 90, 0))
     .cylinder(0.072, 0.18, (0, 0, 0.07), "car_paint", segments=12, rotation=(0, 90, 0))
     .cylinder(0.074, 0.02, (-0.125, 0, 0.07), "umbrella_black", segments=12, rotation=(0, 90, 0))
     .cylinder(0.074, 0.02, (0.125, 0, 0.07), "umbrella_black", segments=12, rotation=(0, 90, 0))
     .sphere(0.01, (0.06, -0.06, 0.12), "moon_rim", segments=6, rings=4, smooth=True)
     .build())
    return relic
