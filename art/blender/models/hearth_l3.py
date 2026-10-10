"""Hearth, level 3 (thatched upper floor, junk pipes, water wheel). Level 1: (spec: the Marchlands). A reclaimed Roman bath ruin: a stone plinth, broken marble columns,
a surviving bit of architrave, a steaming pool, a blue tarp tied over the leaky corner, and a brazier that is
the actual hearth. `Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.buildings import hearth_banya, hearth_ruin, hearth_upper
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l3")
    ruin = Part("Ruin", parent=hearth)
    hearth_ruin(ruin)
    hearth_banya(ruin)
    hearth_upper(ruin)
    ruin_obj = ruin.build()

    (Part("Fire", pivot=(-0.25, -0.25, 0.36), parent=ruin_obj)
     .cylinder(0.11, 0.3, (-0.25, -0.25, 0.5), "fire", segments=7, radius_top=0.0, smooth=True)
     .cylinder(0.07, 0.22, (-0.2, -0.22, 0.47), "ember", segments=6, radius_top=0.0, smooth=True, rotation=(8, 10, 0))
     .build())
    (Part("Steam", pivot=(0.12, 0.05, 0.3), parent=ruin_obj)
     .sphere(0.14, (0.08, 0.05, 0.42), "linen", segments=7, rings=5, smooth=True)
     .sphere(0.11, (0.18, 0.08, 0.6), "linen", segments=7, rings=5, smooth=True)
     .sphere(0.08, (0.1, 0.12, 0.76), "linen", segments=6, rings=4, smooth=True)
     .build())
    return hearth
