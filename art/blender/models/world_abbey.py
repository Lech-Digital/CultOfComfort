"""An abbey on the world map: a stone chapel with a bell-cote, a walled cloister garth with a well, and a vegetable
patch where the monks grow cabbages and grudges."""
from lib.buildings import thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 2500
REQUIRED_PARTS = ("Building",)


def build():
    abbey = root("world_abbey")
    b = Part("Building", parent=abbey)
    b.box((0.44, 0.95, 0.46), (-0.32, 0.1, 0.29), "worn_stone", bevel=0.02)                              # chapel
    b.prism((0.54, 1.0, 0.32), (-0.32, 0.1, 0.68), "fieldstone")
    b.box((0.18, 0.12, 0.34), (-0.32, -0.38, 0.92), "worn_stone", bevel=0.01)                            # bell-cote
    b.box((0.08, 0.13, 0.12), (-0.32, -0.38, 0.95), "tarred_wood", bevel=0.0)
    b.sphere(0.03, (-0.32, -0.38, 0.92), "gold_trim", segments=6, rings=4)
    b.box((0.16, 0.04, 0.26), (-0.32, -0.37, 0.18), "tarred_wood", bevel=0.005)
    for y in (-0.1, 0.25, 0.5):
        b.box((0.03, 0.08, 0.16), (-0.1, y, 0.32), "window", bevel=0.004)
    # The cloister: low walls round a square garth with a well.
    for (x, y, w, l) in ((0.3, -0.25, 0.68, 0.08), (0.3, 0.5, 0.68, 0.08), (0.62, 0.12, 0.08, 0.7)):
        b.box((w, l, 0.24), (x, y, 0.15), "daub", bevel=0.01)
    thatch_roof(b, 0.08, 0.7, (0.62, 0.12, 0.27), 0.08, overhang=0.04, ridge=False, lean=0)
    b.box((0.5, 0.6, 0.02), (0.3, 0.12, 0.07), "foliage", bevel=0.0)                                     # the garth
    b.cylinder(0.08, 0.12, (0.3, 0.12, 0.12), "fieldstone", segments=8)
    b.cylinder(0.06, 0.02, (0.3, 0.12, 0.18), "water", segments=8)
    # Cabbages in rows behind.
    for k in range(4):
        for j in range(2):
            b.sphere(0.05, (0.0 + k * 0.14, 0.8 + j * 0.12, 0.08), "foliage", segments=6, rings=4, scale=(1, 1, 0.7))
    b.build()
    return abbey
