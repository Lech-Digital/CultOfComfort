"""A villager: long hair, headscarf, apron, a bucket for the bathhouse queue. Sceptical but tempted."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Bucket",)


def _scarf_and_apron(parts, d):
    r, c, s = d.r, d.head_z, d.s
    parts["Head"].sphere(r * 1.1, (0, 0.05, c + r * 0.25), "zealot_red", segments=14, rings=8, scale=(1.04, 0.98, 0.75), smooth=True)
    parts["Body"].box((0.26 * s, 0.03, 0.4 * s), (0, -0.14 * s, 0.25 * s), "linen", bevel=0.01)
    for k in range(3):  # a braid down her back, out from under the scarf
        parts["Head"].sphere(r * 0.22, (0, r * 0.8, c - r * (0.2 + k * 0.38)), "thatch", segments=7, rings=5, smooth=True)


def _bucket(parts, d):
    x = -d.arm_x
    bucket = prop(parts, "Bucket", attach_to="ArmR", pivot=(x, 0, d.hand))
    bucket.cylinder(0.07, 0.11, (x, 0, d.hand - 0.1), "old_wood", segments=9, radius_top=0.085)
    bucket.cylinder(0.065, 0.01, (x, 0, d.hand - 0.05), "water", segments=9)
    bucket.cylinder(0.006, 0.16, (x, 0, d.hand - 0.02), "mail", segments=4, rotation=(0, 90, 0))


LOOK = Look(height=0.78, hair="thatch", hair_style="none", brows=6, mouth=0.004, torso="daub", robe=True,
            legs="daub", feet="tarred_wood", belt="old_wood", extras=[_scarf_and_apron, _bucket])


def build():
    return build_person("villager_woman", LOOK)
