"""The blacksmith: leather apron, hammer, forearms like hams. Converted when the Prophet explained pipes."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _apron(parts, d):
    s, g = d.s, d.girth
    parts["Body"].box((0.3 * s * g, 0.03, 0.42 * s), (0, -0.14 * s * g, 0.3 * s), "tarred_wood", bevel=0.01)


def _hammer(parts, d):
    x = -d.arm_x
    hammer = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    hammer.cylinder(0.014, 0.3, (x, -0.04, d.hand + 0.02), "fresh_log", segments=6)
    hammer.box((0.12, 0.06, 0.06), (x, -0.04, d.hand + 0.17), "fieldstone", bevel=0.008)


LOOK = Look(height=0.84, girth=1.35, hair="tarred_wood", hair_style="bald", brows=-8, beard=True, torso="levy_wool",
            legs="old_wood", feet="tarred_wood", belt="tarred_wood", extras=[_apron, _hammer])


def build():
    return build_person("blacksmith", LOOK)
