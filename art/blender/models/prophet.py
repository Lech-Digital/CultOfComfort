"""The Prophet: a tired time traveller from 2024. Chibi like everyone, but a head taller (vegetables).

Built on lib.people (owner-approved friendly face). His 2024 junk (neon sandals, orange belt bag, hi-vis
smartwatch, umbrella) stays crisp; the man himself is rumpled linen and weary brows.
"""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2500  # the remodel's bathrobe, shirt and sunglasses
REQUIRED_PARTS = PERSON_PARTS + ("Umbrella",)


def _belt_bag(parts, d):
    parts["Body"].box((0.12, 0.06, 0.08), (0.08, -0.145, 0.345), "cone_orange", bevel=0.015)


def _smartwatch(parts, d):
    parts["ArmL"].box((0.10, 0.11, 0.03), (d.arm_x, 0, 0.39), "hivis_yellow", bevel=0.006)  # flat battery since 947


def _teal_shirt(parts, d):
    # The remodel's Prophet (art/concepts/hearth-final.jpg): a teal T-shirt under an open white bathrobe.
    body = parts["Body"]
    body.box((0.15, 0.03, 0.2), (0, -0.155, 0.47), "car_paint", bevel=0.01)                       # the shirt in the robe's V
    for side in (-1, 1):
        body.box((0.05, 0.035, 0.24), (0.085 * side, -0.16, 0.46), "linen", bevel=0.012,
                 rotation=(0, -14 * side, 0))                                                    # the robe's lapels


def _sunglasses(parts, d):
    # Pushed up on his head: 2024 does not do sun in 947.
    head = parts["Head"]
    z = d.head_z + d.r * 0.66
    y = -d.r * 0.7
    for side in (-1, 1):
        head.box((0.1, 0.03, 0.06), (0.062 * side, y, z), "umbrella_black", bevel=0.01, rotation=(-30, 0, 0))  # lenses
    head.box((0.26, 0.03, 0.022), (0, y - 0.006, z + 0.03), "flipflop_pink", bevel=0.006, rotation=(-30, 0, 0))  # the frame


def _umbrella(parts, d):
    x = -d.arm_x
    (prop(parts, "Umbrella", attach_to="ArmR", pivot=(x, 0, d.hand))
     .box((0.035, 0.035, 0.06), (x, -0.04, 0.40), "fresh_log", bevel=0.008)                 # wooden handle
     .cylinder(0.012, 0.38, (x, -0.04, 0.18), "umbrella_black", segments=6)                 # shaft (tip on the ground)
     .cylinder(0.02, 0.26, (x, -0.04, 0.17), "umbrella_black", segments=8, radius_top=0.06))  # furled canopy


LOOK = Look(
    height=1.0,
    skin="prophet_skin",
    hair="old_wood",
    hair_style="messy",
    brows=12,
    torso="linen",      # the bathrobe
    legs="linen",
    feet="flipflop_pink",
    head_tilt=-12,  # chin up, a weary sigh; also turns the face towards the high game camera
    extras=[_belt_bag, _smartwatch, _umbrella, _teal_shirt, _sunglasses],
)


def build():
    return build_person("prophet", LOOK)
