"""Hearth, level 4: The Baths of Comfort. Everything of the Temple, and the Comfort Era: a raised cistern with copper
pipes to the pool, a sauna hut steaming at the back, and the Duck Ascendant over it all. See lib/hearth.py.
`Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.hearth import build_hearth
from lib.mesh import root

TRIANGLE_BUDGET = 12000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l4")
    build_hearth(hearth, 4)
    return hearth
