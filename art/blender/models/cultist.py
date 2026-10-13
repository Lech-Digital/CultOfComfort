"""A cultist: a former turnip farmer in clean linen, smiling for the first time in years (hot water will do that)."""
from lib.people import REQUIRED_PARTS, Look, build_person

TRIANGLE_BUDGET = 2200


def _sash(parts, d):
    parts["Body"].box((0.06 * d.s, 0.27 * d.s, 0.3 * d.s), (0.06 * d.s, 0, 0.42 * d.s), "woad", bevel=0.01, rotation=(0, 30, 0))


LOOK = Look(height=0.8, hair="thatch", hair_style="short", brows=4, mouth=0.012, torso="linen", legs="daub",
            feet="fresh_log", belt="woad", extras=[_sash])  # woad sash, dyed the colour of the sacred Tarp


def build():
    return build_person("cultist", LOOK)
