"""Friendly farm creatures for the Marchlands, sharing PartAnimator's rig names so they animate for free.

Quadrupeds: Body (pivot at the ground under the body), Head (neck), front legs ArmL/ArmR, back legs LegL/LegR
(the walk cycle swings ArmL with LegR: a diagonal trot), Tail. Birds: wings are ArmL/ArmR (they flap), legs
LegL/LegR. Authoring space: Z up, facing −Y, the animal's left at +X. Eyes follow the owner-approved style:
rounded, with a highlight.
"""
import math
from dataclasses import dataclass, field

from lib.mesh import Part, marker, root

QUADRUPED_PARTS = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Front")
BIRD_PARTS = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Front")


@dataclass
class Beast:
    body: tuple = (0.36, 0.6, 0.32)      # width X, length Y, height Z
    leg: float = 0.18                     # leg length (ground to body underside)
    leg_r: float = 0.05
    coat: str = "linen"
    belly: str | None = None
    legs: str | None = None               # defaults to coat
    hooves: str = "tarred_wood"
    head_r: float = 0.14
    head_up: float = 0.08                 # head centre above the body top line
    snout: tuple | None = (0.08, 0.07)    # radius, length; None for flat faces
    snout_colour: str | None = None
    nose: str = "tarred_wood"
    ears: str = "flop"                    # flop | point | none
    ear_colour: str | None = None
    tail: str = "short"                   # short | curly | long | none
    tail_angle: float = -65.0             # long tails: degrees from vertical, negative leans back (−25 = proud cat, 160 = hangs)
    eye_size: float = 1.0
    smooth: bool = True                   # fur and skin read friendlier smooth
    extras: list = field(default_factory=list)


def _eyes(head, cx, cy, cz, r, size, spread=0.55, height=0.25):
    for side in (-1, 1):
        x, z = cx + side * r * spread, cz + r * height
        y = cy - r * 0.78
        e = r * 0.2 * size
        head.sphere(e, (x, y, z), "umbrella_black", segments=8, rings=5, scale=(0.85, 0.5, 1.1), smooth=True)
        head.sphere(e * 0.32, (x - e * 0.3, y - e * 0.35, z + e * 0.45), "linen", segments=6, rings=4, smooth=True)


def build_quadruped(name, b):
    animal = root(name)
    w, l, h = b.body
    underside = b.leg
    centre_z = underside + h / 2
    body = Part("Body", pivot=(0, 0, 0), parent=animal)
    body.sphere(0.5, (0, 0, centre_z), b.coat, segments=12, rings=8, scale=(w, l, h), smooth=b.smooth)
    if b.belly:
        body.sphere(0.5, (0, 0.02, centre_z - h * 0.18), b.belly, segments=10, rings=6, scale=(w * 0.85, l * 0.8, h * 0.7), smooth=b.smooth)
    if b.tail == "short":
        body.sphere(0.04, (0, l / 2, centre_z + h * 0.15), b.coat, segments=6, rings=4, smooth=True)
    elif b.tail == "curly":
        body.cylinder(0.02, 0.1, (0, l / 2 + 0.03, centre_z + h * 0.2), b.snout_colour or b.coat, segments=6,
                      rotation=(-60, 0, 0), smooth=True)
        body.sphere(0.035, (0.0, l / 2 + 0.07, centre_z + h * 0.28), b.snout_colour or b.coat, segments=6, rings=4, smooth=True)
    elif b.tail == "long":
        # Rotate the tail about its root at the rump: direction (0, −sinθ, cosθ) for a rotation θ about X.
        length, theta = l * 0.5, math.radians(b.tail_angle)
        start_y, start_z = l / 2 - 0.02, centre_z + h * 0.15
        mid = (0, start_y - math.sin(theta) * length / 2, start_z + math.cos(theta) * length / 2)
        body.cylinder(0.025, length, mid, b.coat, segments=6, radius_top=0.012, rotation=(b.tail_angle, 0, 0), smooth=True)

    head_z = underside + h + b.head_up
    head_y = -l / 2 - b.head_r * 0.3
    head = Part("Head", pivot=(0, -l * 0.38, underside + h * 0.85))
    head.sphere(b.head_r, (0, head_y, head_z), b.coat, segments=12, rings=8, smooth=b.smooth)
    face_y = head_y
    if b.snout:
        sr, sl = b.snout
        head.cylinder(sr, sl, (0, head_y - b.head_r * 0.85, head_z - b.head_r * 0.25), b.snout_colour or b.coat,
                      segments=10, rotation=(90, 0, 0), smooth=True)
        for side in (-1, 1):
            head.sphere(sr * 0.18, (side * sr * 0.38, head_y - b.head_r * 0.85 - sl / 2 - 0.004, head_z - b.head_r * 0.2),
                        b.nose, segments=6, rings=4, smooth=True)
    _eyes(head, 0, face_y, head_z, b.head_r, b.eye_size)
    ear = b.ear_colour or b.coat
    for side in (-1, 1):
        if b.ears == "flop":
            head.sphere(b.head_r * 0.42, (side * b.head_r * 0.85, head_y + 0.01, head_z + b.head_r * 0.35), ear,
                        segments=8, rings=5, scale=(0.35, 0.8, 1.0), rotation=(0, side * 40, 0), smooth=True)
        elif b.ears == "point":
            head.cylinder(b.head_r * 0.28, b.head_r * 0.6, (side * b.head_r * 0.55, head_y + 0.01, head_z + b.head_r * 0.95),
                          ear, segments=6, radius_top=0.0, rotation=(0, side * 15, 0), smooth=True)

    legs_colour = b.legs or b.coat
    leg_parts = {}
    for leg_name, x, y in (("ArmL", w * 0.3, -l * 0.3), ("ArmR", -w * 0.3, -l * 0.3), ("LegL", w * 0.3, l * 0.3), ("LegR", -w * 0.3, l * 0.3)):
        leg = Part(leg_name, pivot=(x, y, underside + h * 0.2))
        leg.cylinder(b.leg_r, underside + h * 0.2, (x, y, (underside + h * 0.2) / 2), legs_colour, segments=8, smooth=b.smooth)
        leg.cylinder(b.leg_r * 1.1, 0.04, (x, y, 0.02), b.hooves, segments=8)
        leg_parts[leg_name] = leg

    parts = {"Body": body, "Head": head, **leg_parts}
    for extra in b.extras:
        extra(parts, b)
    body_obj = body.build()
    for part in (head, *leg_parts.values()):
        part.parent = body_obj
        part.build()
    marker("Front", (0, -l / 2 - b.head_r - 0.1, centre_z), animal)
    return animal


@dataclass
class Bird:
    size: float = 1.0
    feathers: str = "linen"
    wing: str | None = None
    beak: str = "cone_orange"
    legs: str = "cone_orange"
    comb: str | None = None               # chicken comb colour
    neck: float = 0.0                     # extra neck length (geese)
    extras: list = field(default_factory=list)


def build_bird(name, b):
    bird = root(name)
    s = b.size
    body = Part("Body", pivot=(0, 0, 0), parent=bird)
    body.sphere(0.13 * s, (0, 0.02 * s, 0.2 * s), b.feathers, segments=12, rings=8, scale=(0.9, 1.2, 0.85), smooth=True)
    body.cylinder(0.06 * s, 0.1 * s, (0, 0.16 * s, 0.26 * s), b.feathers, segments=8, radius_top=0.01, rotation=(-55, 0, 0), smooth=True)  # tail
    if b.neck:
        body.cylinder(0.045 * s, b.neck * s, (0, -0.1 * s, 0.28 * s + b.neck * s / 2), b.feathers, segments=8, rotation=(12, 0, 0), smooth=True)
    head_z = 0.36 * s + b.neck * s
    head = Part("Head", pivot=(0, -0.08 * s, 0.28 * s + b.neck * s))
    head.sphere(0.075 * s, (0, -0.11 * s, head_z), b.feathers, segments=10, rings=7, smooth=True)
    head.cylinder(0.03 * s, 0.07 * s, (0, -0.2 * s, head_z - 0.01 * s), b.beak, segments=8, radius_top=0.003, rotation=(90, 0, 0))
    _eyes(head, 0, -0.11 * s, head_z, 0.075 * s, 1.15, spread=0.6, height=0.2)
    if b.comb:
        head.sphere(0.03 * s, (0, -0.11 * s, head_z + 0.075 * s), b.comb, segments=6, rings=4, scale=(0.5, 1.4, 1.0), smooth=True)
        head.sphere(0.02 * s, (0, -0.17 * s, head_z - 0.06 * s), b.comb, segments=6, rings=4, smooth=True)  # wattle
    wings = {}
    for wing_name, side in (("ArmL", 1), ("ArmR", -1)):
        wing = Part(wing_name, pivot=(side * 0.11 * s, 0, 0.25 * s))
        wing.sphere(0.085 * s, (side * 0.11 * s, 0.04 * s, 0.19 * s), b.wing or b.feathers, segments=8, rings=6,
                    scale=(0.3, 1.15, 0.6), smooth=True)  # folded against the body
        wings[wing_name] = wing
    legs = {}
    for leg_name, side in (("LegL", 1), ("LegR", -1)):
        leg = Part(leg_name, pivot=(side * 0.045 * s, 0, 0.1 * s))
        leg.cylinder(0.012 * s, 0.1 * s, (side * 0.045 * s, 0, 0.05 * s), b.legs, segments=5)
        leg.box((0.06 * s, 0.08 * s, 0.012 * s), (side * 0.045 * s, -0.02 * s, 0.006 * s), b.legs, bevel=0.003)
        legs[leg_name] = leg
    parts = {"Body": body, "Head": head, **wings, **legs}
    for extra in b.extras:
        extra(parts, b)
    body_obj = body.build()
    for part in (head, *wings.values(), *legs.values()):
        part.parent = body_obj
        part.build()
    marker("Front", (0, -0.3 * s, 0.2 * s), bird)
    return bird
