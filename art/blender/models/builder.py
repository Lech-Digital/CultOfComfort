"""The cult's builder (owner 2026-10-14: "a worker using sticks and mud and grass… or a hammer and a piece of wood"):
a cultist in a leather apron with a mallet, a reed tucked behind the ear. He stands at whatever is being built and
hammers; `Hammer` swings with `ArmR`."""
from lib.people import REQUIRED_PARTS as PERSON_PARTS, Look, build_person, prop

TRIANGLE_BUDGET = 2600
REQUIRED_PARTS = PERSON_PARTS + ("Hammer",)


def _apron(parts, d):
    parts["Body"].box((0.3 * d.s, 0.03, 0.3 * d.s), (0, -0.13 * d.s, 0.36 * d.s), "old_wood", bevel=0.008)
    parts["Body"].box((0.14 * d.s, 0.035, 0.07 * d.s), (0.06 * d.s, -0.14 * d.s, 0.33 * d.s), "tarred_wood", bevel=0.004)  # pocket of pegs


def _mallet(parts, d):
    x = -d.arm_x
    hammer = prop(parts, "Hammer", attach_to="ArmR", pivot=(x, 0, d.hand))
    hammer.box((0.03, 0.24 * d.s, 0.03), (x, -0.1 * d.s, d.hand), "fresh_log", bevel=0.005)          # handle, held forward
    hammer.box((0.08, 0.08, 0.15 * d.s), (x, -0.22 * d.s, d.hand), "old_wood", bevel=0.01)            # mallet head


def _reed(parts, d):
    parts["Head"].cylinder(0.008, 0.2 * d.s, (d.r * 0.9, 0.02, d.head_z + 0.04), "thatch", segments=4, rotation=(0, 30, 0))


LOOK = Look(height=0.8, hair="thatch", hair_style="messy", brows=-4, mouth=0.008, torso="linen", legs="daub",
            feet="fresh_log", belt="woad", extras=[_apron, _mallet, _reed])


def build():
    return build_person("builder", LOOK)
