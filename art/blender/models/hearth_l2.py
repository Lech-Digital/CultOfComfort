"""Hearth, level 2: Camp Comfort. The wheels are off (one rings the fire, one is the sign), the engine block is an altar,
a wing mirror watches the road, a log banya is bolted on, the first clay-and-woad copies appear. See lib/hearth.py.
`Fire` glows with the Hearth's warmth; `Steam` drifts."""
from lib.hearth import build_hearth
from lib.mesh import root

TRIANGLE_BUDGET = 10000
REQUIRED_PARTS = ("Ruin", "Fire", "Steam")


def build():
    hearth = root("hearth_l2")
    build_hearth(hearth, 2)
    return hearth
