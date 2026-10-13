"""Gatehouse: two squat stone towers, an arch, a portcullis and a little roof. Wulfric's men will need a ladder. And a plan."""
from lib.buildings import thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 2500
REQUIRED_PARTS = ("Prop",)


def build():
    house = root("gatehouse")
    p = Part("Prop", parent=house)
    for x in (-0.42, 0.42):
        p.box((0.3, 0.42, 1.4), (x, 0, 0.7), "fieldstone", bevel=0.03)                       # towers
        for k in (-1, 1):
            p.box((0.1, 0.44, 0.14), (x + k * 0.1, 0, 1.47), "worn_stone", bevel=0.01)        # crenels
    p.box((0.56, 0.4, 0.3), (0, 0, 1.15), "worn_stone", bevel=0.02)                          # arch top
    for k in range(5):
        p.box((0.03, 0.04, 0.95), (-0.22 + k * 0.11, -0.12, 0.5), "mail", bevel=0.0)         # portcullis bars
    for z in (0.3, 0.65):
        p.box((0.5, 0.04, 0.03), (0, -0.12, z), "mail", bevel=0.0)
    thatch_roof(p, 0.56, 0.4, (0, 0, 1.3), 0.3)
    p.box((0.2, 0.02, 0.32), (0, -0.21, 1.1), "zealot_red", bevel=0.005)                       # the village banner
    p.build()
    return house
