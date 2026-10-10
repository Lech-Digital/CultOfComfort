"""The Prophet: a tired time traveller from 2024. Chibi like everyone, but a head taller (vegetables).

Built on lib.people (owner-approved friendly face). His 2024 junk (neon sandals, orange belt bag, hi-vis
smartwatch, umbrella) stays crisp; the man himself is rumpled linen and weary brows.
"""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Umbrella",)


def _belt_bag(parts, d):
    parts["Body"].box((0.12, 0.06, 0.08), (0.08, -0.145, 0.345), "cone_orange", bevel=0.015)


def _smartwatch(parts, d):
    parts["ArmL"].box((0.10, 0.11, 0.03), (d.arm_x, 0, 0.39), "hivis_yellow", bevel=0.006)  # flat battery since 947


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
    torso="linen",
    legs="linen",
    feet="flipflop_pink",
    head_tilt=-12,  # chin up, a weary sigh; also turns the face towards the high game camera
    extras=[_belt_bag, _smartwatch, _umbrella],
)


def build():
    return build_person("prophet", LOOK)
