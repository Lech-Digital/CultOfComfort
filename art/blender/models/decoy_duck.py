"""The Decoy Duck: a rubber duck the size of a pig, carved from a log and painted yellow, on a little cart. No
raider has ever walked past one. It isn't the real duck."""
from lib.buildings import rubber_duck, wheel
from lib.mesh import Part, root

TRIANGLE_BUDGET = 2600
REQUIRED_PARTS = ("Prop",)


def build():
    decoy = root("decoy_duck")
    p = Part("Prop", parent=decoy)
    p.box((0.7, 0.5, 0.08), (0, 0, 0.18), "old_wood", bevel=0.01)                                            # cart bed
    for x in (-0.25, 0.25):
        wheel(p, (x, -0.28, 0.14), 0.13, spokes=4, rim=8, width=0.05)
        wheel(p, (x, 0.28, 0.14), 0.13, spokes=4, rim=8, width=0.05)
    rubber_duck(p, (0, 0, 0.22), size=2.6, yaw=20)
    p.build()
    return decoy
