"""Zealot: Friar Odo's flock. Tonsure, red robe, rope belt, sandals, eyes too wide, smile too sure,
and a hymnal he will throw at the heretic."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _hymnal(parts, d):
    x = -d.arm_x
    book = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    book.box((0.13, 0.05, 0.16), (x, -0.06, d.hand + 0.02), "tarred_wood", bevel=0.01)          # cover
    book.box((0.115, 0.055, 0.145), (x, -0.06, d.hand + 0.02), "linen", bevel=0.004)             # pages
    book.box((0.02, 0.056, 0.08), (x, -0.06 - 0.003, d.hand + 0.03), "gold_trim", bevel=0.002)   # gilt cross


def _cord(parts, d):
    parts["Body"].cylinder(0.012, 0.22 * d.s, (0.06, -0.14 * d.s, 0.25 * d.s), "thatch", segments=5)  # dangling rope end


LOOK = Look(
    height=0.78,
    hair="old_wood",
    hair_style="tonsure",
    brows=-6,
    eye_size=1.25,     # fervour
    mouth=0.012,       # unsettlingly sure smile
    torso="zealot_red",
    robe=True,
    legs="zealot_red",
    feet="fresh_log",  # bare-ish sandals
    belt="thatch",
    extras=[_hymnal, _cord],
)


def build():
    return build_person("zealot", LOOK)
