"""Man-at-arms: Wulfric's professional. Nasal helmet, mail, red surcoat with gold trim, kite shield, sword,
a moustache with ambitions, and the permanent scowl of a man paid by the day."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon", "Shield")


def _helmet(parts, d):
    r, c = d.r, d.head_z
    head = parts["Head"]
    head.box((r * 2.0, r * 1.2, r * 0.7), (0, 0.06 * r / 0.2, c - r * 0.55), "mail", bevel=0.03, bevel_segments=2)  # mail neck guard
    # Norman helm: a dome over the crown (rim above the brows so the face shows), a point on top.
    head.sphere(r * 1.04, (0, 0.015, c + r * 0.2), "fieldstone", segments=12, rings=7, scale=(1.04, 1.0, 0.92), smooth=True)
    head.cylinder(r * 0.4, r * 0.55, (0, 0.015, c + r * 1.18), "fieldstone", segments=10, radius_top=0.0, smooth=True)
    head.cylinder(r * 1.08, r * 0.08, (0, 0.015, c + r * 0.42), "worn_stone", segments=12)
    head.box((0.03, 0.03, r * 0.5), (0, d.face_y(0, c + r * 0.2, inset=-0.02), c + r * 0.15), "fieldstone", bevel=0.006)  # nasal bar


def _surcoat(parts, d):
    s, g = d.s, d.girth
    body = parts["Body"]
    body.box((0.36 * s * g, 0.26 * s * g, 0.3 * s), (0, 0, 0.4 * s), "wulfric_red", bevel=0.04 * s, bevel_segments=2)
    body.box((0.37 * s * g, 0.27 * s * g, 0.035 * s), (0, 0, 0.33 * s), "gold_trim", bevel=0.008)


def _shield(parts, d):
    x = d.arm_x + 0.07
    shield = prop(parts, "Shield", attach_to="ArmL", pivot=(d.arm_x, 0, d.hand))
    shield.box((0.04, 0.26 * d.s, 0.34 * d.s), (x, -0.04, d.hand + 0.05), "wulfric_red", bevel=0.02, bevel_segments=2)
    shield.box((0.045, 0.04, 0.3 * d.s), (x, -0.04, d.hand + 0.05), "gold_trim", bevel=0.005)     # cross, upright
    shield.box((0.045, 0.22 * d.s, 0.04), (x, -0.04, d.hand + 0.1), "gold_trim", bevel=0.005)     # cross, arm


def _sword(parts, d):
    x = -d.arm_x
    sword = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    sword.box((0.035, 0.012, 0.36 * d.s), (x, -0.06, d.hand - 0.04), "mail", bevel=0.004)         # blade, held down
    sword.box((0.1, 0.03, 0.025), (x, -0.06, d.hand + 0.15), "gold_trim", bevel=0.005)            # crossguard
    sword.box((0.03, 0.03, 0.06), (x, -0.06, d.hand + 0.2), "tarred_wood", bevel=0.005)          # grip


LOOK = Look(
    height=0.86,
    girth=1.2,
    hair="old_wood",
    hair_style="none",
    brows=-14,          # cross
    moustache=True,
    torso="mail",
    sleeves="mail",
    legs="mail",
    feet="tarred_wood",
    belt=None,
    head_detail=(12, 8),  # mostly under the helmet
    extras=[_helmet, _surcoat, _shield, _sword],
)


def build():
    return build_person("manatarms", LOOK)
