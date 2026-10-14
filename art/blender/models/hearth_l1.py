"""Hearth, level 1: The Landing. The Roman bath ruin on its 4×4 plot, the teal car nose-down in the dry plunge pool
(bonnet up, a door torn off), the fire in a hubcap between two car seats, the tarp over the camp. See lib/hearth.py.
`Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.hearth import build_hearth
from lib.mesh import root

TRIANGLE_BUDGET = 10000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l1")
    build_hearth(hearth, 1)
    return hearth
