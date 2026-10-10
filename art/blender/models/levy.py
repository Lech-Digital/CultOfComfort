"""Levy: one of Lord Wulfric's conscripted farmhands. Hood, pitchfork, red armband, worried about all of it."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _hood(parts, d):
    r, c = d.r, d.head_z
    head = parts["Head"]
    head.sphere(r * 1.12, (0, 0.07 * r / 0.2, c + 0.03), "levy_wool", segments=16, rings=10, scale=(1.05, 0.95, 0.95), smooth=True)
    head.cylinder(r * 0.35, r * 0.9, (0, 0.17 * r / 0.2, c + r * 0.75), "levy_wool", segments=8, radius_top=0.02,
                  rotation=(-35, 0, 0), smooth=True)                                          # floppy hood tip
    parts["Body"].box((0.42 * d.s, 0.3 * d.s, 0.07 * d.s), (0, 0.01, 0.55 * d.s), "levy_wool", bevel=0.03 * d.s)  # hood drape


def _armband(parts, d):
    parts["ArmL"].box((0.1 * d.s, 0.11 * d.s, 0.04 * d.s), (d.arm_x, 0, 0.5 * d.s), "wulfric_red", bevel=0.006)


def _pitchfork(parts, d):
    x = -d.arm_x
    fork = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    fork.cylinder(0.014, 0.85 * d.s, (x, -0.03, d.hand + 0.08), "fresh_log", segments=6)      # handle
    top = d.hand + 0.08 + 0.425 * d.s
    fork.box((0.12, 0.02, 0.02), (x, -0.03, top), "mail", bevel=0.004)                         # crossbar
    for dx in (-0.05, 0, 0.05):
        fork.cylinder(0.008, 0.12, (x + dx, -0.03, top + 0.06), "mail", segments=5, radius_top=0.002)


LOOK = Look(
    height=0.8,
    hair=None,
    hair_style="none",
    brow_colour="old_wood",
    brows=16,          # worried
    mouth=-0.008,      # frown: he did not sign up for this
    torso="levy_wool",
    legs="old_wood",
    feet="tarred_wood",
    belt="thatch",     # rope
    extras=[_hood, _armband, _pitchfork],
)


def build():
    return build_person("levy", LOOK)
