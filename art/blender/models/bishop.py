"""The Bishop: tall mitre, white and gold robes, a crozier, and grave concerns about the heathen bathhouse."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _mitre_and_stole(parts, d):
    r, c, s = d.r, d.head_z, d.s
    head = parts["Head"]
    head.prism((r * 1.5, r * 1.3, r * 1.4), (0, 0.0, c + r * 1.3), "roman_marble", rotation=(0, 0, 90))  # mitre, peak front-to-back
    head.box((r * 0.18, r * 1.32, r * 1.2), (0, 0.0, c + r * 1.2), "gold_trim", bevel=0.003)
    body = parts["Body"]
    body.box((0.08 * s, 0.27 * s, 0.55 * s), (0.08 * s, -0.005, 0.32 * s), "gold_trim", bevel=0.008)   # stole
    body.box((0.08 * s, 0.27 * s, 0.55 * s), (-0.08 * s, -0.005, 0.32 * s), "gold_trim", bevel=0.008)


def _crozier(parts, d):
    x = -d.arm_x
    staff = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    staff.cylinder(0.012, 1.0 * d.s, (x, -0.04, d.hand + 0.14), "gold_trim", segments=6)
    staff.cylinder(0.05, 0.015, (x, -0.04, d.hand + 0.66 * d.s), "gold_trim", segments=8, rotation=(90, 0, 0))  # crook (a hoop)


LOOK = Look(height=0.86, hair="worn_stone", hair_style="short", brows=14, mouth=-0.01, torso="roman_marble",
            robe=True, legs="roman_marble", feet="tarred_wood", belt="gold_trim", extras=[_mitre_and_stole, _crozier])


def build():
    return build_person("bishop", LOOK)
