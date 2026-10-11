"""A spa acolyte: towel turban, towel round the shoulders, flip-flops someone carved out of bark. Blissful."""
from lib.people import REQUIRED_PARTS, Look, build_person

TRIANGLE_BUDGET = 2200


def _towels(parts, d):
    r, c, s = d.r, d.head_z, d.s
    head = parts["Head"]
    head.sphere(r * 1.08, (0, 0.03, c + r * 0.35), "linen", segments=14, rings=8, scale=(1.05, 1.0, 0.7), smooth=True)
    head.sphere(r * 0.6, (0.02, 0.0, c + r * 0.95), "linen", segments=10, rings=6, scale=(1.0, 1.0, 0.75), smooth=True)
    parts["Body"].box((0.44 * s, 0.32 * s, 0.07 * s), (0, 0, 0.57 * s), "linen", bevel=0.03 * s, bevel_segments=2)


LOOK = Look(height=0.8, hair=None, hair_style="none", brow_colour="old_wood", brows=-4, mouth=0.016,
            torso="tarp_blue", robe=True, legs="tarp_blue", feet="fresh_log", belt="linen", extras=[_towels])


def build():
    return build_person("cultist_towel", LOOK)
