"""A goose: the Marchlands' most dangerous creature. Long neck, orange beak, no fear."""
from lib.animals import BIRD_PARTS, Bird, build_bird

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = BIRD_PARTS

LOOK = Bird(size=1.25, feathers="roman_marble", wing="worn_stone", beak="cone_orange", legs="cone_orange", neck=0.26)


def build():
    return build_bird("goose", LOOK)
