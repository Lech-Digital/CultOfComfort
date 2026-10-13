"""Marchland building blocks (spec: the Marchlands). Medieval things are wonky: walls and roofs lean a little."""
import math

from lib.mesh import Part


def log_wall(part, start, end, height, z0=0.0, radius=0.06, swatch="fresh_log", ends=True):
    """Horizontal logs stacked between two (x, y) points, Slavic-cabin style, with log ends poking out."""
    (x0, y0), (x1, y1) = start, end
    length = math.hypot(x1 - x0, y1 - y0)
    yaw = math.degrees(math.atan2(y1 - y0, x1 - x0))
    count = max(1, int(height / (radius * 1.9)))
    for k in range(count):
        z = z0 + radius + k * radius * 1.9
        wobble = (k % 3 - 1) * 0.6
        part.cylinder(radius, length + (radius * 2.4 if ends else 0), ((x0 + x1) / 2, (y0 + y1) / 2, z),
                      swatch if k % 2 == 0 else "old_wood", segments=7, rotation=(0, 90, yaw + wobble))


def thatch_roof(part, width, length, centre, height, overhang=0.12, ridge=True, lean=1.5):
    """Gable roof of thatch with a darker ridge; `centre` is the eaves-level centre, ridge along Y."""
    cx, cy, cz = centre
    part.prism((width + overhang * 2, length + overhang * 2, height), (cx, cy, cz + height / 2), "thatch", rotation=(0, 0, lean))
    if ridge:
        part.cylinder(0.05, length + overhang * 2.2, (cx, cy, cz + height + 0.01), "old_wood", segments=6, rotation=(90, 0, lean))


def plank_roof(part, width, length, centre, slope_deg, swatch="tarred_wood"):
    """A single sloping board roof (lean-to): high at +Y, low at −Y."""
    cx, cy, cz = centre
    for k in range(max(2, int(width / 0.16))):
        x = cx - width / 2 + 0.08 + k * 0.16
        part.box((0.15, length, 0.03), (x, cy, cz), swatch if k % 2 else "old_wood", bevel=0.005, rotation=(slope_deg, 0, (k % 3 - 1) * 1.2))


def wattle_fence(part, start, end, height=0.45, z0=0.0):
    """Woven hurdle fence between two points: stakes plus woven bands."""
    (x0, y0), (x1, y1) = start, end
    length = math.hypot(x1 - x0, y1 - y0)
    yaw = math.degrees(math.atan2(y1 - y0, x1 - x0))
    stakes = max(2, int(length / 0.35) + 1)
    for k in range(stakes):
        t = k / (stakes - 1)
        part.cylinder(0.02, height + 0.08, (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z0 + (height + 0.08) / 2), "old_wood", segments=5)
    for band in range(3):
        part.box((length, 0.035, height / 4), ((x0 + x1) / 2, (y0 + y1) / 2, z0 + height * (0.25 + band * 0.27)),
                 "daub" if band % 2 else "fresh_log", bevel=0.006, rotation=(0, 0, yaw))


def barrel(part, centre, radius=0.12, height=0.3):
    x, y, z = centre
    part.cylinder(radius, height, (x, y, z + height / 2), "old_wood", segments=9, radius_top=radius * 0.92)
    for t in (0.2, 0.8):
        part.cylinder(radius * 1.03, 0.025, (x, y, z + height * t), "fieldstone", segments=9)


def sack(part, centre, swatch="daub"):
    x, y, z = centre
    part.sphere(0.12, (x, y, z + 0.1), swatch, segments=8, rings=6, scale=(1.0, 0.85, 0.95), smooth=True)
    part.cylinder(0.035, 0.06, (x, y, z + 0.22), swatch, segments=6, smooth=True)


# ---------------- the Hearth, grown by accretion (spec: Hearth evolution) ----------------

def hearth_ruin(ruin):
    """Level 1: the reclaimed Roman bath ruin."""
    ruin.box((1.6, 1.4, 0.18), (0, 0, 0.09), "worn_stone", bevel=0.03, rotation=(0, 0, 1))            # plinth
    ruin.box((1.5, 0.12, 0.08), (0, -0.66, 0.2), "roman_marble", bevel=0.015)                         # front step
    ruin.cylinder(0.42, 0.04, (0.12, 0.05, 0.19), "roman_marble", segments=14)                        # pool lip
    ruin.cylinder(0.36, 0.04, (0.12, 0.05, 0.205), "water", segments=14)                              # the water
    for x, y, h in ((-0.62, -0.5, 1.15), (-0.62, 0.5, 0.75), (0.62, 0.5, 1.2), (0.62, -0.5, 0.45)):
        ruin.cylinder(0.09, h, (x, y, 0.18 + h / 2), "roman_marble", segments=10)
        ruin.cylinder(0.12, 0.06, (x, y, 0.2), "worn_stone", segments=10)
    ruin.cylinder(0.09, 0.3, (0.3, -0.42, 0.27), "roman_marble", segments=10, rotation=(90, 0, 30))  # fallen drum
    ruin.box((0.2, 1.25, 0.14), (-0.62, 0.0, 1.38), "roman_marble", bevel=0.02, rotation=(0, -4, 0))  # architrave
    ruin.box((0.23, 0.6, 0.06), (-0.62, -0.25, 1.48), "worn_stone", bevel=0.01, rotation=(0, -4, 0))
    ruin.cylinder(0.02, 0.9, (0.75, -0.62, 0.6), "old_wood", segments=5)                              # tarp pole
    ruin.box((0.72, 0.9, 0.02), (0.55, -0.08, 1.22), "tarp_blue", bevel=0, rotation=(-14, 9, 0))      # the tarp
    ruin.cylinder(0.008, 0.5, (0.78, -0.45, 1.02), "thatch", segments=4, rotation=(20, 0, 0))
    for k in range(3):                                                                                 # first log patch
        ruin.cylinder(0.06, 0.9, (0.1, 0.62, 0.3 + k * 0.12), "fresh_log", segments=7, rotation=(0, 90, 0))
    ruin.cylinder(0.14, 0.12, (-0.25, -0.25, 0.27), "fieldstone", segments=9, radius_top=0.18)        # brazier
    ruin.cylinder(0.15, 0.03, (-0.25, -0.25, 0.34), "tarred_wood", segments=9)


def hearth_banya(part):
    """Level 2: a Slavic log banya bolted onto the back of the ruin, shingle-ish roof, stone chimney, second tarp."""
    for wall in (((0.1, 0.62), (0.95, 0.62)), ((0.95, 0.62), (0.95, 1.35)), ((0.95, 1.35), (0.1, 1.35)), ((0.1, 1.35), (0.1, 0.62))):
        log_wall(part, *wall, height=0.62, z0=0.18, radius=0.055)
    thatch_roof(part, 0.85, 0.73, (0.52, 0.985, 0.8), 0.42, lean=-2)
    part.box((0.18, 0.18, 0.75), (0.82, 1.2, 0.95), "fieldstone", bevel=0.02, rotation=(0, 2, 0))     # chimney
    part.box((0.36, 0.5, 0.02), (0.3, 1.05, 1.08), "tarp_blue", bevel=0, rotation=(8, -22, 10))       # a second tarp, obviously
    part.box((0.18, 0.04, 0.34), (0.52, 0.6, 0.36), "tarred_wood", bevel=0.01)                        # banya door
    part.box((0.1, 0.03, 0.06), (0.78, 0.6, 0.56), "window", bevel=0.005)                              # glowing window


def hearth_upper(part):
    """Level 3: a thatched upper floor on daub walls above the ruin, junk 'copper' pipes into the pool,
    and a little water wheel."""
    part.box((0.9, 0.7, 0.42), (-0.3, 0.1, 1.68), "daub", bevel=0.03, rotation=(0, 0, -2))            # upper floor walls
    for x in (-0.72, 0.12):
        part.box((0.06, 0.72, 0.44), (x, 0.1, 1.68), "old_wood", bevel=0.01)                          # timber frame
    part.box((0.92, 0.06, 0.06), (-0.3, -0.25, 1.89), "old_wood", bevel=0.01)
    thatch_roof(part, 0.9, 0.7, (-0.3, 0.1, 1.9), 0.45, lean=2)
    part.box((0.12, 0.03, 0.12), (-0.5, -0.26, 1.7), "window", bevel=0.005)
    # Pipes (gold_trim stands in for salvaged copper) from the banya chimney down into the pool.
    part.cylinder(0.03, 0.7, (0.55, 0.62, 0.9), "gold_trim", segments=6, rotation=(0, 90, 0))
    part.cylinder(0.03, 0.6, (0.2, 0.62, 0.62), "gold_trim", segments=6)
    part.cylinder(0.03, 0.5, (0.2, 0.38, 0.33), "gold_trim", segments=6, rotation=(90, 0, 0))
    part.cylinder(0.05, 0.05, (0.2, 0.15, 0.33), "cone_orange", segments=6, rotation=(90, 0, 0))     # a tap. A 2024 tap.
    # Water wheel on the right side, turned by nothing in particular.
    part.cylinder(0.32, 0.08, (1.02, -0.1, 0.42), "old_wood", segments=10, rotation=(0, 90, 0))
    part.cylinder(0.06, 0.2, (1.02, -0.1, 0.42), "tarred_wood", segments=6, rotation=(0, 90, 0))
    for k in range(4):
        part.box((0.1, 0.62, 0.05), (1.02, -0.1, 0.42), "fresh_log", bevel=0.005, rotation=(k * 45, 0, 0))


# ---------------- upgrade kit: what a building collects as it levels up (spec: Muddy Satire) ----------------

def rubber_duck(part, centre, size=1.0, yaw=0):
    """The cult's minor deity of bathing, at any size: from a bath toy to a rooftop idol."""
    x, y, z = centre
    s = size
    part.sphere(0.12 * s, (x, y + 0.02 * s, z + 0.1 * s), "hivis_yellow", segments=9, rings=6, scale=(1.0, 1.3, 0.8), smooth=True,
                rotation=(0, 0, yaw))
    part.sphere(0.075 * s, (x, y - 0.1 * s, z + 0.22 * s), "hivis_yellow", segments=8, rings=6, smooth=True)
    part.cylinder(0.03 * s, 0.06 * s, (x, y - 0.18 * s, z + 0.21 * s), "cone_orange", segments=8, radius_top=0.02 * s, rotation=(90, 0, 0))
    for side in (-1, 1):
        part.sphere(0.014 * s, (x + side * 0.04 * s, y - 0.16 * s, z + 0.25 * s), "umbrella_black", segments=6, rings=4, smooth=True)


def towel_line(part, start, end, height, colours):
    """Two posts and a sagging line of towels between them, facing the camera (−Y)."""
    (x0, y0), (x1, y1) = start, end
    length = math.hypot(x1 - x0, y1 - y0)
    yaw = math.degrees(math.atan2(y1 - y0, x1 - x0))
    for x, y in (start, end):
        part.cylinder(0.02, height, (x, y, height / 2), "old_wood", segments=5)
    part.cylinder(0.005, length, ((x0 + x1) / 2, (y0 + y1) / 2, height - 0.03), "thatch", segments=4, rotation=(0, 90, yaw))
    for k, colour in enumerate(colours):
        t = (k + 0.5) / len(colours)
        sag = 0.05 * math.sin(math.pi * t)
        part.box((length / len(colours) * 0.8, 0.015, 0.17), (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, height - 0.12 - sag), colour,
                 bevel=0.003, rotation=(0, 0, yaw + (k % 2 * 2 - 1) * 3))


def flip_flops(part, centre, yaw=0):
    x, y, z = centre
    for side in (-1, 1):
        part.box((0.05, 0.11, 0.012), (x + side * 0.035, y, z + 0.006), "flipflop_pink", bevel=0.004, rotation=(0, 0, yaw + side * 8))


def wheel(part, centre, radius, spokes=8, swatch="fresh_log", rim=12, width=0.08):
    """A wheel standing in the XZ plane (facing the camera), axle along Y: water wheels, treadwheels, windlasses."""
    cx, cy, cz = centre
    for k in range(rim):
        a = 2 * math.pi * k / rim
        part.box((2 * math.pi * radius / rim * 1.08, width, 0.05), (cx + radius * math.cos(a), cy, cz + radius * math.sin(a)),
                 "old_wood" if k % 2 else swatch, bevel=0.006, rotation=(0, -(math.degrees(a) + 90), 0))
    for k in range(spokes):
        a = 2 * math.pi * k / spokes
        part.box((radius, 0.04, 0.035), (cx + radius / 2 * math.cos(a), cy, cz + radius / 2 * math.sin(a)), "old_wood",
                 bevel=0.004, rotation=(0, -math.degrees(a), 0))
    part.cylinder(0.05, width + 0.12, (cx, cy, cz), "tarred_wood", segments=6, rotation=(90, 0, 0))


def peat_stack(part, centre, rows=3, yaw=0):
    """A neat drying stack of peat bricks."""
    x, y, z = centre
    for row in range(rows):
        for col in range(max(1, 2 - row // 2)):
            part.box((0.18, 0.1, 0.08), (x + col * 0.05 * (row % 2), y + (col - 0.5) * 0.11, z + 0.04 + row * 0.085),
                     "tarred_wood" if (row + col) % 2 else "wet_mud", bevel=0.01, rotation=(0, 0, yaw + row * 90))
