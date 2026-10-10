"""Old Agnieszka the herb-wife: grey bun, shawl, cane, knows exactly what's in the slurry and won't say."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS
from lib.people import Look, build_person, prop

TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = PERSON_PARTS + ("Weapon",)


def _shawl(parts, d):
    s = d.s
    parts["Body"].box((0.44 * s, 0.32 * s, 0.12 * s), (0, 0, 0.54 * s), "foliage", bevel=0.04 * s, bevel_segments=2)


def _cane(parts, d):
    x = -d.arm_x
    cane = prop(parts, "Weapon", attach_to="ArmR", pivot=(x, 0, d.hand))
    cane.cylinder(0.012, d.hand + 0.02, (x, -0.05, d.hand / 2), "old_wood", segments=6)
    cane.cylinder(0.014, 0.08, (x - 0.03, -0.05, d.hand + 0.02), "old_wood", segments=6, rotation=(0, 90, 0))


LOOK = Look(height=0.72, hair="worn_stone", hair_style="bun", brows=-10, mouth=0.008, torso="old_wood", robe=True,
            legs="old_wood", feet="tarred_wood", belt=None, head_tilt=8, extras=[_shawl, _cane])


def build():
    return build_person("herbwife", LOOK)
