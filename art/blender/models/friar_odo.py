"""Friar Odo: leader of the zealots. Round, red-robed, tonsured, a very big book and a very small mercy."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _book(parts, d):
    x = -d.arm_x
    book = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    book.box((0.2, 0.07, 0.24), (x, -0.08, d.hand + 0.06), "tarred_wood", bevel=0.012)
    book.box((0.18, 0.075, 0.22), (x, -0.08, d.hand + 0.06), "linen", bevel=0.004)
    book.box((0.03, 0.076, 0.13), (x, -0.08 - 0.003, d.hand + 0.08), "gold_trim", bevel=0.003)
    book.box((0.09, 0.076, 0.03), (x, -0.08 - 0.003, d.hand + 0.1), "gold_trim", bevel=0.003)


def _rosary(parts, d):
    for k in range(6):
        parts["Body"].sphere(0.015, (-0.06 + k * 0.024, -0.16 * d.s * d.girth, 0.48 * d.s - abs(k - 2.5) * 0.02), "tarred_wood",
                             segments=5, rings=3, smooth=True)


LOOK = Look(height=0.86, girth=1.4, hair="worn_stone", hair_style="tonsure", brows=-16, eye_size=1.15, mouth=-0.006,
            torso="zealot_red", robe=True, legs="zealot_red", feet="fresh_log", belt="thatch", extras=[_book, _rosary])


def build():
    return build_person("friar_odo", LOOK)
