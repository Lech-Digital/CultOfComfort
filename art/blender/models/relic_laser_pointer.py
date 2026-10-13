"""Relic: a laser pointer (cat toy, 2024). To the Marchlands, the Finger of God: a red dot that goes where the
Prophet points. `Beam` is the dot's ray, for the game to show and hide."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400
REQUIRED_PARTS = ("Prop", "Beam")


def build():
    relic = root("relic_laser_pointer")
    prop = (Part("Prop", parent=relic)
            .cylinder(0.025, 0.24, (0, 0, 0.03), "chrome", segments=10, rotation=(90, 0, 0))
            .box((0.02, 0.05, 0.012), (0, 0.02, 0.055), "umbrella_black", bevel=0.004)
            .sphere(0.012, (0, -0.122, 0.03), "laser_red", segments=6, rings=4, smooth=True)
            .build())
    Part("Beam", pivot=(0, -0.12, 0.03), parent=prop).cylinder(0.006, 0.6, (0, -0.42, 0.03), "laser_red", segments=5, rotation=(90, 0, 0)).build()
    return relic
