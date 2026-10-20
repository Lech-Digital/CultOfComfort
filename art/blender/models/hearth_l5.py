"""Hearth, level 5: across the river. Everything of the Baths, and a golden dome with the Duck Ascendant gilded on
top, standing stones at the corners and banners on every column. See lib/hearth.py.
`Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.hearth import build_hearth
from lib.mesh import root

TRIANGLE_BUDGET = 14000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l5")
    build_hearth(hearth, 5)
    return hearth
