"""Hearth, level 3: The Temple of Comfort. The car on stilts as a shrine with its headlights lit, its hoses feeding the
hot pool; a thatched upper floor, a water wheel, the mosaic restored in woad and madder. See lib/hearth.py.
`Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.hearth import build_hearth
from lib.mesh import root

TRIANGLE_BUDGET = 10000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l3")
    build_hearth(hearth, 3)
    return hearth
