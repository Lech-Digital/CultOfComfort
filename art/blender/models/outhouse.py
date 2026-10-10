"""An outhouse with a crescent moon cut in the door. The Prophet has plans for plumbing. Big plans."""
from lib.buildings import plank_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    loo = root("outhouse")
    part = (Part("Building", parent=loo)
            .box((0.6, 0.6, 1.0), (0, 0, 0.5), "old_wood", bevel=0.02, rotation=(0, 0, 2))
            .box((0.42, 0.03, 0.82), (0, -0.31, 0.45), "fresh_log", bevel=0.01)                                # door
            .sphere(0.05, (0.06, -0.33, 0.78), "tarred_wood", segments=6, rings=4, scale=(1, 0.3, 1)))          # the moon cut-out
    plank_roof(part, 0.7, 0.75, (0, 0, 1.06), -12)
    part.box((0.08, 0.08, 0.08), (0.18, -0.33, 0.45), "mail", bevel=0.01)                                       # latch
    part.build()
    return loo
