"""Lord Wulfric: the local warlord. Big red cloak, gold crown slightly too small, magnificent beard, sword,
the face of a man who has just been told about bathing."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _crown_and_cloak(parts, d):
    r, c, s, g = d.r, d.head_z, d.s, d.girth
    head = parts["Head"]
    head.cylinder(r * 0.62, r * 0.28, (0, 0.02, c + r * 0.98), "gold_trim", segments=10)
    for k in range(5):
        import math
        a = k / 5 * math.tau
        head.cylinder(r * 0.07, r * 0.25, (math.cos(a) * r * 0.55, 0.02 + math.sin(a) * r * 0.55, c + r * 1.2), "gold_trim",
                      segments=4, radius_top=0.0)
    head.sphere(r * 0.09, (0, d.face_y(0, c + r * 0.98, inset=-0.06) + 0.03, c + r * 1.0), "wulfric_red", segments=6, rings=4)  # ruby
    body = parts["Body"]
    body.box((0.46 * s * g, 0.08 * s, 0.56 * s), (0, 0.14 * s * g, 0.33 * s), "wulfric_red", bevel=0.03 * s, bevel_segments=2,
             rotation=(-6, 0, 0))                                                                 # cloak down the back
    body.box((0.48 * s * g, 0.3 * s * g, 0.07 * s), (0, 0.02, 0.58 * s), "linen", bevel=0.03 * s, bevel_segments=2)  # fur collar


def _sword(parts, d):
    x = -d.arm_x
    sword = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    sword.box((0.04, 0.014, 0.42 * d.s), (x, -0.06, d.hand - 0.06), "mail", bevel=0.004)
    sword.box((0.13, 0.035, 0.03), (x, -0.06, d.hand + 0.16), "gold_trim", bevel=0.005)
    sword.sphere(0.025, (x, -0.06, d.hand + 0.24), "gold_trim", segments=6, rings=4)


LOOK = Look(height=0.92, girth=1.3, hair="old_wood", hair_style="bald",  # bald under the crown; all the hair went into the beard
            brows=-18, beard=True, torso="wulfric_red",
            sleeves="mail", legs="mail", feet="tarred_wood", belt="gold_trim", extras=[_crown_and_cloak, _sword])


def build():
    return build_person("lord_wulfric", LOOK)
