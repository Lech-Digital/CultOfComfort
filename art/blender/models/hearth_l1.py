"""Hearth, level 1 (spec: the Marchlands). A reclaimed Roman bath ruin: a stone plinth, broken marble columns,
a surviving bit of architrave, a steaming pool, a blue tarp tied over the leaky corner, and a brazier that is
the actual hearth. `Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l1")
    ruin = Part("Ruin", parent=hearth)
    ruin.box((1.6, 1.4, 0.18), (0, 0, 0.09), "worn_stone", bevel=0.03, rotation=(0, 0, 1))            # plinth
    ruin.box((1.5, 0.12, 0.08), (0, -0.66, 0.2), "roman_marble", bevel=0.015)                         # front step
    # The pool, sunk into the plinth, with a marble lip.
    ruin.cylinder(0.42, 0.04, (0.12, 0.05, 0.19), "roman_marble", segments=14)
    ruin.cylinder(0.36, 0.04, (0.12, 0.05, 0.205), "water", segments=14)
    # Broken columns at different heights (fluted = 10 sides), one fallen drum on the floor.
    for x, y, h in ((-0.62, -0.5, 1.15), (-0.62, 0.5, 0.75), (0.62, 0.5, 1.2), (0.62, -0.5, 0.45)):
        ruin.cylinder(0.09, h, (x, y, 0.18 + h / 2), "roman_marble", segments=10)
        ruin.cylinder(0.12, 0.06, (x, y, 0.2), "worn_stone", segments=10)                             # base
    ruin.cylinder(0.09, 0.3, (0.3, -0.42, 0.27), "roman_marble", segments=10, rotation=(90, 0, 30))  # fallen drum
    ruin.box((0.2, 1.25, 0.14), (-0.62, 0.0, 1.38), "roman_marble", bevel=0.02, rotation=(0, -4, 0))  # architrave
    ruin.box((0.23, 0.6, 0.06), (-0.62, -0.25, 1.48), "worn_stone", bevel=0.01, rotation=(0, -4, 0))
    # The tarp: stretched over the right-hand corner on two poles and the tall column, tied with rope.
    ruin.cylinder(0.02, 0.9, (0.75, -0.62, 0.6), "old_wood", segments=5)
    ruin.box((0.72, 0.9, 0.02), (0.55, -0.08, 1.22), "tarp_blue", bevel=0, rotation=(-14, 9, 0))
    ruin.cylinder(0.008, 0.5, (0.78, -0.45, 1.02), "thatch", segments=4, rotation=(20, 0, 0))
    # A Slavic log patch bolted onto the back: the village grows by accretion.
    for k in range(3):
        ruin.cylinder(0.06, 0.9, (0.1, 0.62, 0.3 + k * 0.12), "fresh_log", segments=7, rotation=(0, 90, 0))
    # The brazier: the actual hearth everyone protects.
    ruin.cylinder(0.14, 0.12, (-0.25, -0.25, 0.27), "fieldstone", segments=9, radius_top=0.18)
    ruin.cylinder(0.15, 0.03, (-0.25, -0.25, 0.34), "tarred_wood", segments=9)
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
