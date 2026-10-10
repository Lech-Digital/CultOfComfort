"""The Prophet: a tired time traveller from 2024. Chibi like everyone, but a head taller (vegetables).

Blender space: Z up, the model faces −Y. 1 unit = 1 m. Skin is smooth-shaded and the face has rounded,
friendly features (owner feedback on v1: faceted head + black rectangles read as scary). 2024 items
(sandals, belt bag, watch, umbrella) stay crisp.
"""
import math

from lib.mesh import Part, marker, root

# Hero exception to the spec's 1,500 per character: one Prophet on screen, and a smooth friendly face costs
# triangles (owner feedback on v1). Locals keep 1,500.
TRIANGLE_BUDGET = 2200
REQUIRED_PARTS = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Umbrella", "Front")

HIP_Z = 0.26
SHOULDER_Z = 0.56
NECK_Z = 0.60
HAND_Z = 0.35
ARM_X = 0.215

HEAD_CENTRE = (0.0, 0.0, 0.79)
HEAD_RADIUS = 0.2
HEAD_SCALE = (1.05, 0.95, 0.97)


def _face_y(x, z, inset=0.004):
    """Y of the head's front surface at (x, z), so features sit on the face instead of floating or sinking."""
    sx, sy, sz = HEAD_SCALE
    dx, dz = x / (HEAD_RADIUS * sx), (z - HEAD_CENTRE[2]) / (HEAD_RADIUS * sz)
    return -HEAD_RADIUS * sy * math.sqrt(max(0.0, 1 - dx * dx - dz * dz)) + inset


def _leg(name, x, body):
    return (Part(name, pivot=(x, 0, HIP_Z), parent=body)
            .box((0.11, 0.12, 0.23), (x, 0, 0.145), "linen", bevel=0.025)                   # rolled-up linen trousers
            .box((0.13, 0.18, 0.03), (x, -0.02, 0.015), "flipflop_pink", bevel=0.008)       # neon 2024 sandals
            .build())


def _arm(name, x, body, watch):
    arm = (Part(name, pivot=(x, 0, SHOULDER_Z), parent=body)
           .box((0.09, 0.10, 0.20), (x, 0, 0.47), "linen", bevel=0.025, rotation=(0, -4 if x > 0 else 4, 0))
           .sphere(0.05, (x, 0, HAND_Z), "prophet_skin", segments=8, rings=6, smooth=True))
    if watch:
        arm.box((0.10, 0.11, 0.03), (x, 0, 0.39), "hivis_yellow", bevel=0.006)                # smartwatch, flat battery
    return arm.build()


def _head(body):
    head = Part("Head", pivot=(0, 0, NECK_Z), parent=body)
    head.sphere(HEAD_RADIUS, HEAD_CENTRE, "prophet_skin", segments=14, rings=10, scale=HEAD_SCALE, smooth=True)

    # Hair: a soft cap sitting on the back and top, plus one cowlick that refuses to lie down.
    head.sphere(0.215, (0, 0.04, 0.865), "old_wood", segments=20, rings=10, scale=(1.03, 0.98, 0.72), smooth=True)
    head.sphere(0.05, (0.03, 0.09, 0.995), "old_wood", segments=6, rings=5, scale=(1.2, 0.8, 0.7), smooth=True)

    # Ears make him read as a person at phone size.
    for side in (-1, 1):
        head.sphere(0.042, (side * 0.205, 0.0, 0.78), "prophet_skin", segments=6, rings=5, scale=(0.45, 0.75, 1.0), smooth=True)

    # Eyes: rounded, set a little low (friendly), with a small highlight.
    for side in (-1, 1):
        x, z = side * 0.075, 0.775
        head.sphere(0.026, (x, _face_y(x, z), z), "umbrella_black", segments=8, rings=5, scale=(0.85, 0.45, 1.15), smooth=True)
        hx, hz = x - 0.008, z + 0.012
        head.sphere(0.008, (hx, _face_y(hx, hz, inset=-0.006), hz), "linen", segments=6, rings=4, smooth=True)

    # Tired eyebrows: inner ends raised, the look of a man who has seen 2024 and then 947.
    for side in (-1, 1):
        x, z = side * 0.075, 0.828
        head.box((0.06, 0.016, 0.014), (x, _face_y(x, z, inset=-0.004), z), "old_wood", bevel=0.005, rotation=(0, side * 12, 0))

    # Small round nose and a short, unimpressed mouth (soft brown, not black).
    head.sphere(0.024, (0, _face_y(0, 0.75, inset=-0.006), 0.75), "prophet_skin", segments=8, rings=6, smooth=True)
    head.box((0.05, 0.012, 0.011), (0, _face_y(0, 0.705, inset=-0.002), 0.705), "wet_mud", bevel=0.004)

    # Chin up, as in a weary sigh; it also turns the face towards the high game camera. Head has no children,
    # so rotating it does not disturb any child pivots.
    return head.build(rotation=(-12, 0, 0))


def build():
    prophet = root("prophet")

    body = (Part("Body", pivot=(0, 0, 0), parent=prophet)
            .box((0.34, 0.24, 0.26), (0, 0, 0.45), "linen", bevel=0.05, bevel_segments=2, rotation=(0, 0, 1.5))  # rumpled shirt
            .box((0.38, 0.27, 0.09), (0, 0, 0.30), "linen", bevel=0.03)                     # untucked hem
            .box((0.36, 0.26, 0.03), (0, 0, 0.355), "umbrella_black", bevel=0.008)          # belt
            .box((0.12, 0.06, 0.08), (0.08, -0.145, 0.345), "cone_orange", bevel=0.015)     # belt bag (2024)
            .build())

    # He faces −Y, so his left is +X.
    _leg("LegL", 0.08, body)
    _leg("LegR", -0.08, body)
    _head(body)
    _arm("ArmL", ARM_X, body, watch=True)
    arm_r = _arm("ArmR", -ARM_X, body, watch=False)

    (Part("Umbrella", pivot=(-ARM_X, 0, HAND_Z), parent=arm_r)
     .box((0.035, 0.035, 0.06), (-ARM_X, -0.04, 0.40), "fresh_log", bevel=0.008)           # wooden handle
     .cylinder(0.012, 0.38, (-ARM_X, -0.04, 0.18), "umbrella_black", segments=6)           # shaft (tip on the ground)
     .cylinder(0.02, 0.26, (-ARM_X, -0.04, 0.17), "umbrella_black", segments=8, radius_top=0.06)  # furled canopy, chunkier
     .build())

    marker("Front", (0, -0.3, 0.5), prophet)
    return prophet
