"""A chicken: small, brown, red comb, permanently alarmed."""
from lib.animals import BIRD_PARTS, Bird, build_bird

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = BIRD_PARTS

LOOK = Bird(size=0.9, feathers="fresh_log", wing="old_wood", beak="gold_trim", legs="gold_trim", comb="zealot_red")


def build():
    return build_bird("chicken", LOOK)
