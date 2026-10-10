"""Friendly chibi people (spec: Style guide rules 2 and 5, owner-approved on the Prophet).

`build_person(name, look)` makes the standard rig every character shares, so `PartAnimator` can animate any of
them: root → Body (pivot at feet) → Head (neck), ArmL/ArmR (shoulders), LegL/LegR (hips), plus a `Front` marker.
Authoring space: Z up, facing −Y, the person's left at +X (lib.mesh turns it to face Unity +Z).
Accessories are added through `Look.extras`: callables `(parts, dims)` run before each part is built, where
`parts` maps part name → Part (not yet built) and `dims` holds the scaled measurements.
"""
import math
from dataclasses import dataclass, field

from lib.mesh import Part, marker, root

REQUIRED_PARTS = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Front")


@dataclass
class Look:
    height: float = 0.8              # metres, top of head; locals ≈ 0.8, the Prophet 1.0
    girth: float = 1.0               # torso width multiplier (1.2 = well-fed man-at-arms)
    skin: str = "local_skin"
    hair: str | None = "old_wood"    # swatch, or None
    hair_style: str = "short"        # short | messy | tonsure | bald | none (hat/hood/helmet supplies the top)
    brows: float = 10.0              # degrees; > 0 inner ends up (tired, worried), < 0 down (cross)
    brow_colour: str | None = None   # defaults to the hair colour
    mouth: float = 0.0               # curvature in metres-ish: > 0 smile, < 0 frown, 0 flat
    eye_size: float = 1.0            # 1.2 = wide-eyed zealot
    moustache: bool = False
    torso: str = "linen"
    sleeves: str | None = None       # defaults to torso
    legs: str = "linen"
    feet: str = "tarred_wood"
    belt: str | None = "umbrella_black"
    robe: bool = False               # ankle-length robe instead of tunic + trousers
    head_tilt: float = 0.0           # authoring-space chin pose, e.g. -12 = chin up
    head_detail: tuple = (14, 10)    # head sphere segments/rings; lower it when a helmet or hood covers most of it
    extras: list = field(default_factory=list)


@dataclass
class Dims:
    """Measurements for one person, scaled from the Prophet (1.0 m)."""
    s: float          # body scale
    r: float          # head radius
    hip: float
    shoulder: float
    neck: float
    hand: float
    arm_x: float
    leg_x: float
    head_z: float     # head centre height
    girth: float

    def face_y(self, x, z, inset=0.004):
        """Y of the head's front surface at (x, z) so features sit on the face."""
        sx, sy, sz = HEAD_SCALE
        dx, dz = x / (self.r * sx), (z - self.head_z) / (self.r * sz)
        return -self.r * sy * math.sqrt(max(0.0, 1 - dx * dx - dz * dz)) + inset


HEAD_SCALE = (1.05, 0.95, 0.97)


def dims_for(look):
    s = look.height  # body proportions scale with height ...
    r = 0.2 * math.sqrt(s)  # ... but heads shrink slower: chibi
    neck = 0.60 * s
    return Dims(s=s, r=r, hip=0.26 * s, shoulder=0.56 * s, neck=neck, hand=0.35 * s,
                arm_x=0.215 * s * (0.6 + 0.4 * look.girth), leg_x=0.08 * s * look.girth,
                head_z=neck + r * 0.95, girth=look.girth)


def build_person(name, look):
    d = dims_for(look)
    person = root(name)
    s, g = d.s, d.girth
    sleeves = look.sleeves or look.torso

    body = Part("Body", pivot=(0, 0, 0), parent=person)
    if look.robe:
        body.box((0.36 * s * g, 0.26 * s * g, 0.58 * s), (0, 0, 0.31 * s), look.torso, bevel=0.04 * s, bevel_segments=2)
        body.box((0.42 * s * g, 0.30 * s * g, 0.12 * s), (0, 0, 0.06 * s), look.torso, bevel=0.03 * s)   # hem flare
    else:
        body.box((0.34 * s * g, 0.24 * s * g, 0.26 * s), (0, 0, 0.45 * s), look.torso, bevel=0.05 * s, bevel_segments=2,
                 rotation=(0, 0, 1.5))
        body.box((0.38 * s * g, 0.27 * s * g, 0.09 * s), (0, 0, 0.30 * s), look.torso, bevel=0.03 * s)
    if look.belt:
        body.box((0.36 * s * g + 0.01, 0.26 * s * g + 0.01, 0.03 * s), (0, 0, 0.355 * s), look.belt, bevel=0.008)

    head = Part("Head", pivot=(0, 0, d.neck), parent=None)
    legs = {side: Part(side, pivot=(x, 0, d.hip)) for side, x in (("LegL", d.leg_x), ("LegR", -d.leg_x))}
    arms = {side: Part(side, pivot=(x, 0, d.shoulder)) for side, x in (("ArmL", d.arm_x), ("ArmR", -d.arm_x))}
    parts = {"Body": body, "Head": head, **legs, **arms}

    _face(head, look, d)
    for side, part in legs.items():
        x = d.leg_x if side == "LegL" else -d.leg_x
        if look.robe:
            part.box((0.10 * s, 0.10 * s, 0.06 * s), (x, 0, 0.04 * s), look.legs, bevel=0.015)   # just feet poke out
        else:
            part.box((0.11 * s, 0.12 * s, 0.23 * s), (x, 0, 0.145 * s), look.legs, bevel=0.025 * s)
        part.box((0.13 * s, 0.18 * s, 0.03 * s), (x, -0.02 * s, 0.015 * s), look.feet, bevel=0.008)
    for side, part in arms.items():
        x = d.arm_x if side == "ArmL" else -d.arm_x
        lean = -4 if x > 0 else 4
        part.box((0.09 * s, 0.10 * s, 0.20 * s), (x, 0, 0.47 * s), sleeves, bevel=0.025 * s, rotation=(0, lean, 0))
        part.sphere(0.05 * s * (0.8 + 0.2 * g), (x, 0, d.hand), look.skin, segments=8, rings=6, smooth=True)

    for extra in look.extras:
        extra(parts, d)

    body_obj = body.build()
    for part in (head, *legs.values(), *arms.values()):
        part.parent = body_obj
    built = {"Body": body_obj, "Head": head.build(rotation=(look.head_tilt, 0, 0))}
    for name_, part in (*legs.items(), *arms.items()):
        built[name_] = part.build()
    for name_, part in parts.items():
        if name_ not in built:  # extra parts (weapons, shields) declared by accessories
            part.parent = built[part.attach_to] if hasattr(part, "attach_to") else body_obj
            built[name_] = part.build()

    marker("Front", (0, -0.3 * s, 0.5 * s), person)
    return person


def _face(head, look, d):
    r, c, k = d.r, d.head_z, d.r / 0.2  # k scales face offsets designed on the Prophet's 0.2 m head
    head.sphere(r, (0, 0, c), look.skin, segments=look.head_detail[0], rings=look.head_detail[1], scale=HEAD_SCALE, smooth=True)
    hair = look.hair
    brow = look.brow_colour or hair or "old_wood"

    if hair and look.hair_style in ("short", "messy"):
        head.sphere(r * 1.075, (0, 0.04 * k, c + 0.075 * k), hair, segments=20, rings=10, scale=(1.03, 0.98, 0.72), smooth=True)
        if look.hair_style == "messy":
            head.sphere(0.05 * k, (0.03 * k, 0.09 * k, c + 0.205 * k), hair, segments=6, rings=5, scale=(1.2, 0.8, 0.7), smooth=True)
    elif hair and look.hair_style == "tonsure":
        # A ring of hair low around the back and sides; the bald crown pokes through on top.
        head.sphere(r * 1.06, (0, 0.035 * k, c + 0.035 * k), hair, segments=20, rings=10, scale=(1.03, 0.98, 0.7), smooth=True)

    for side in (-1, 1):
        head.sphere(0.042 * k, (side * 0.205 * k, 0.0, c - 0.01 * k), look.skin, segments=6, rings=5, scale=(0.45, 0.75, 1.0), smooth=True)

    for side in (-1, 1):
        x, z = side * 0.075 * k, c - 0.015 * k
        e = 0.026 * k * look.eye_size
        head.sphere(e, (x, d.face_y(x, z), z), "umbrella_black", segments=8, rings=5, scale=(0.85, 0.45, 1.15), smooth=True)
        hx, hz = x - 0.008 * k, z + 0.012 * k * look.eye_size
        head.sphere(0.008 * k * look.eye_size, (hx, d.face_y(hx, hz, inset=-0.006), hz), "linen", segments=6, rings=4, smooth=True)

    for side in (-1, 1):
        x, z = side * 0.075 * k, c + 0.038 * k + 0.01 * k * (look.eye_size - 1)
        # side = +1 is his left; the inner end is towards the nose, so "inner up" tilts the brows opposite ways.
        head.box((0.06 * k, 0.016 * k, 0.014 * k), (x, d.face_y(x, z, inset=-0.004), z), brow, bevel=0.005,
                 rotation=(0, side * look.brows, 0))

    head.sphere(0.024 * k, (0, d.face_y(0, c - 0.04 * k, inset=-0.006), c - 0.04 * k), look.skin, segments=8, rings=6, smooth=True)

    if look.moustache:
        for side in (-1, 1):
            x, z = side * 0.035 * k, c - 0.07 * k
            head.sphere(0.035 * k, (x, d.face_y(x, z, inset=-0.01), z), hair or "old_wood", segments=8, rings=5,
                        scale=(1.4, 0.6, 0.6), smooth=True)
    else:
        # Three short segments; the outer ones lift for a smile or drop for a frown.
        z = c - 0.085 * k
        head.box((0.032 * k, 0.012 * k, 0.011 * k), (0, d.face_y(0, z, inset=-0.002), z), "wet_mud", bevel=0.004)
        for side in (-1, 1):
            x, zz = side * 0.022 * k, z + look.mouth * k * 0.5  # overlaps the centre piece: one continuous line
            head.box((0.024 * k, 0.012 * k, 0.011 * k), (x, d.face_y(x, zz, inset=-0.002), zz), "wet_mud", bevel=0.004,
                     rotation=(0, -side * math.degrees(math.atan2(look.mouth, 0.02)), 0))


def prop(parts, name, attach_to, pivot):
    """Declares an extra animated part (weapon, shield, book) parented to an existing part when built."""
    part = Part(name, pivot=pivot)
    part.attach_to = attach_to
    parts[name] = part
    return part
