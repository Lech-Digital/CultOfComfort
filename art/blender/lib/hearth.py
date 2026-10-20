"""The Hearth (owner 2026-10-14: "way bigger… roman ruins, and the half-disassembled car the Prophet came in").

A 4×4-tile plot, authored 1 unit = 1 tile, centred on the origin, front (the camera) at −Y. The fire burns in the
middle. Level by level the cult takes the car apart and builds the Hearth out of it:

1. The Landing: a sunken Roman bath, the teal car nose-down in its dry plunge pool, bonnet up, one door torn off and
   propped by the fire as a windbreak. The fire burns in a hubcap; the front seats are pulled out round it; the tarp
   is strung over the camp.
2. Camp Comfort: the wheels are off (one rings the fire, one stands up as a sign), the engine block is an altar with
   candles, a wing mirror on a pole watches the road, wattle patches the walls, a log banya is bolted on. The first
   copies of the relics appear, in clay and woad.
3. The Temple of Comfort: the car is raised on timber stilts as a shrine (the Chariot), its headlights lit, its
   radiator hoses feeding a hot pool in the old bath; a thatched upper floor, a water wheel, the mosaic restored
   in woad and madder.
"""
import math

from lib.buildings import log_wall, rubber_duck, thatch_roof, wattle_fence, wheel
from lib.car import bonnet_hinge, bonnet_lid, boot_hinge, boot_lid, car_body, car_door, car_seat
from lib.mesh import Part

HALF = 1.85  # the plinth's half-size: a hair inside the 4×4 plot
POOL = (0.3, -1.75, 1.72, -0.38)  # the plunge pool: min x, min y, max x, max y
FIRE = (0.0, 0.0)


def rod(part, a, b, radius, swatch, segments=6):
    """A straight cylinder from point `a` to point `b` (hoses, poles, ropes)."""
    dx, dy, dz = (b[i] - a[i] for i in range(3))
    length = math.sqrt(dx * dx + dy * dy + dz * dz)
    beta = math.degrees(math.acos(max(-1.0, min(1.0, dz / length))))
    gamma = math.degrees(math.atan2(dy, dx))
    centre = tuple((a[i] + b[i]) / 2 for i in range(3))
    part.cylinder(radius, length, centre, swatch, segments=segments, rotation=(0, beta, gamma))


def ruin(part, level):
    """The Roman bath every level stands on: plinth, mosaic, broken walls and columns, the plunge pool."""
    part.box((HALF * 2, HALF * 2, 0.14), (0, 0, 0.07), "worn_stone", bevel=0.04, rotation=(0, 0, 0.6))
    for x, y, w, d in ((-1.7, -0.6, 0.35, 0.5), (1.65, 0.3, 0.3, 0.4), (-0.4, 1.72, 0.5, 0.25), (0.9, 1.75, 0.4, 0.2)):
        part.box((w, d, 0.1), (x, y, 0.06), "fieldstone", bevel=0.02, rotation=(0, 0, 7))  # chipped edge slabs

    _mosaic(part, level)
    if level < 3:
        for (x0, y0), (x1, y1) in (((-1.3, -1.2), (-0.5, -0.7)), ((-0.9, 0.6), (-0.3, 1.3)), ((0.2, 0.3), (0.9, 0.2))):
            length = math.hypot(x1 - x0, y1 - y0)
            part.box((length, 0.025, 0.012), ((x0 + x1) / 2, (y0 + y1) / 2, 0.145), "tarred_wood", bevel=0,
                     rotation=(0, 0, math.degrees(math.atan2(y1 - y0, x1 - x0))))  # cracks
        for x, y, r in ((-1.1, -0.35, 0.16), (-0.25, -0.95, 0.12), (0.95, 0.05, 0.14), (-0.9, 1.15, 0.12), (-0.15, 1.35, 0.1), (1.3, 0.9, 0.13)):
            part.cylinder(r, 0.05, (x, y, 0.16), "grass_alt", segments=7, radius_top=r * 0.6)  # grass through the floor

    # Walls: the back and the left survive in stumps. The raiders' road comes in through the gap at the back,
    # just left of the middle (the fire's tile is the plot's north-west centre one), so nothing stands there.
    for x0, x1, h in ((-1.78, -1.05, 1.0), (0.02, 0.4, 0.3)):
        part.box((x1 - x0, 0.24, h), ((x0 + x1) / 2, 1.68, 0.14 + h / 2), "worn_stone", bevel=0.03, rotation=(0, 0, 0.8))
    for y0, y1, h in ((0.55, 1.6, 0.8), (-0.25, 0.55, 0.4)):
        part.box((0.24, y1 - y0, h), (-1.68, (y0 + y1) / 2, 0.14 + h / 2), "worn_stone", bevel=0.03, rotation=(0, 0, -0.6))
    for x, y, r in ((0.6, 1.62, 0.12), (0.85, 1.5, 0.09), (-1.65, -0.5, 0.1), (-1.55, -0.75, 0.08)):
        part.sphere(r, (x, y, 0.16), "worn_stone", segments=6, rings=4)  # rubble

    # Columns: two still carry a scrap of architrave; the rest are broken off.
    for x, y, h in ((-1.6, 1.52, 2.1), (-1.12, 1.52, 2.1), (1.58, 1.58, 1.35), (-1.6, -1.58, 0.85)):
        part.cylinder(0.17, 0.1, (x, y, 0.19), "worn_stone", segments=10)
        part.cylinder(0.12, h, (x, y, 0.24 + h / 2), "roman_marble", segments=10)
        if h > 2:
            part.box((0.34, 0.34, 0.1), (x, y, 0.29 + h), "roman_marble", bevel=0.02)  # capital
    part.box((0.78, 0.3, 0.2), (-1.36, 1.52, 2.44), "roman_marble", bevel=0.02, rotation=(0, -3, 0))
    for k in range(2):
        part.box((0.7, 0.2, 0.06), (-0.5, 1.75 - k * 0.2, 0.16 - k * 0.02), "worn_stone", bevel=0.01)  # the worn threshold
    part.cylinder(0.12, 0.38, (-1.25, -1.0, 0.26), "roman_marble", segments=10, rotation=(90, 0, 35))  # fallen drum

    # A toppled marble head in the grass at the front, nose to the sky.
    part.sphere(0.22, (-1.12, -1.68, 0.3), "roman_marble", segments=9, rings=6, scale=(1.0, 0.9, 1.15), rotation=(0, 80, 0))
    part.box((0.07, 0.12, 0.06), (-1.12, -1.75, 0.5), "roman_marble", bevel=0.015, rotation=(-20, 0, 0))  # nose
    part.cylinder(0.24, 0.05, (-1.12, -1.62, 0.36), "fieldstone", segments=8, rotation=(0, 80, 0))  # laurel band

    # The plunge pool: a marble lip, mud (later, hot water) inside, two steps down.
    x0, y0, x1, y1 = POOL
    part.box((x1 - x0, 0.16, 0.12), ((x0 + x1) / 2, y0, 0.2), "roman_marble", bevel=0.02)
    part.box((x1 - x0, 0.16, 0.12), ((x0 + x1) / 2, y1, 0.2), "roman_marble", bevel=0.02)
    part.box((0.16, y1 - y0, 0.12), (x0, (y0 + y1) / 2, 0.2), "roman_marble", bevel=0.02)
    part.box((0.16, y1 - y0, 0.12), (x1, (y0 + y1) / 2, 0.2), "roman_marble", bevel=0.02)
    part.box((x1 - x0 - 0.16, y1 - y0 - 0.16, 0.04), ((x0 + x1) / 2, (y0 + y1) / 2, 0.15), "water" if level >= 3 else "wet_mud", bevel=0)
    if level < 3:
        part.box((0.4, 0.3, 0.012), (1.35, -0.7, 0.175), "water", bevel=0)  # a puddle, all that is left of the bath
    for k in range(2):
        part.box((0.36, 0.14, 0.05), (0.62, y1 - 0.15 - k * 0.14, 0.2 - k * 0.04), "worn_stone", bevel=0.01)


def _mosaic(part, level):
    """A ring of tiles round the fire: cracked and gappy until Hearth 3 restores it with the cult's own sign."""
    count = 16
    for k in range(count):
        if level < 3 and k % 3 == 1:
            continue
        a = 2 * math.pi * k / count
        colours = ("woad", "madder") if level >= 3 else ("roman_marble", "ochre_clay", "tarred_wood")
        part.box((0.2, 0.2, 0.012), (0.7 * math.cos(a), 0.7 * math.sin(a), 0.146), colours[k % len(colours)], bevel=0,
                 rotation=(0, 0, math.degrees(a)))
    if level >= 3:  # the sign: a sun (the Hearth) in a bath (comfort)
        for k in range(8):
            a = 2 * math.pi * k / 8
            part.box((0.18, 0.06, 0.012), (0.48 * math.cos(a), 0.48 * math.sin(a), 0.147), "weld", bevel=0, rotation=(0, 0, math.degrees(a)))


def camp(part, level):
    """Round the fire: the ring it burns in, two car seats, the door as a windbreak, the boot's last treasures."""
    fx, fy = FIRE
    if level >= 2:  # a wheel, tyre and all, rings the fire
        part.cylinder(0.32, 0.11, (fx, fy, 0.2), "umbrella_black", segments=12)
        part.cylinder(0.24, 0.115, (fx, fy, 0.2), "fieldstone", segments=12)
    for k in range(7):
        a = 2 * math.pi * k / 7
        part.sphere(0.07, (fx + 0.36 * math.cos(a), fy + 0.36 * math.sin(a), 0.17), "fieldstone", segments=6, rings=4)
    part.cylinder(0.2, 0.04, (fx, fy, 0.27 if level >= 2 else 0.17), "chrome", segments=12)  # the hubcap brazier

    car_seat(part, (-0.66, 0.02, 0.14), yaw=90)
    car_seat(part, (0.64, 0.12, 0.14), yaw=-90)
    car_door(part, (0.02, 0.62, 0.14), s=1.3, lean=-6)

    # The cool box from the boot (still cold, somehow) and the warning cone.
    part.box((0.3, 0.2, 0.18), (-0.42, -1.25, 0.23), "linen", bevel=0.02, rotation=(0, 0, 12))
    part.box((0.31, 0.21, 0.05), (-0.42, -1.25, 0.34), "tarp_blue", bevel=0.015, rotation=(0, 0, 12))
    part.box((0.18, 0.18, 0.025), (0.12, -1.72, 0.16), "umbrella_black", bevel=0.005)
    part.cylinder(0.07, 0.28, (0.12, -1.72, 0.31), "cone_orange", segments=8, radius_top=0.015)
    part.cylinder(0.05, 0.04, (0.12, -1.72, 0.32), "linen", segments=8)

    if level == 1:
        # The tarp, strung from the back column to a pole at the pool.
        rod(part, (0.6, 0.95, 0.14), (0.6, 0.95, 1.45), 0.025, "old_wood")
        part.box((1.6, 1.0, 0.02), (-0.35, 1.0, 1.55), "tarp_blue", bevel=0, rotation=(-8, -6, 3))
        rod(part, (0.6, 0.95, 1.45), (1.0, -0.6, 0.85), 0.006, "thatch", segments=4)  # guy rope to the car's aerial


def landed_car(parent, level):
    """The time machine as each level leaves it. Returns the car object."""
    if level >= 3:
        return _chariot(parent)
    s = 1.15
    nose = (1.02, -1.42, 0.12)
    car = Part("TimeMachine", pivot=nose, parent=parent)
    car_body(car, nose, s, wheels=level < 2, door=False, bonnet=False, coil=True)
    car_obj = car.build(rotation=(34, 0, 18))  # nose-down in the pool, tail over the lip
    lid = Part("BonnetUp", pivot=bonnet_hinge(nose, s), parent=car_obj)
    bonnet_lid(lid, nose, s)
    lid.build(rotation=(-80, 0, 0))
    boot = Part("BootOpen", pivot=boot_hinge(nose, s), parent=car_obj)
    boot_lid(boot, nose, s)
    boot.build(rotation=(-70, 0, 0))
    return car_obj


def _chariot(parent):
    """Hearth 3: the car, level again, on timber stilts as a shrine. Headlights lit, hoses into the hot pool."""
    s = 1.15
    nose = (-1.12, -1.72, 0.62)
    stilts = Part("Stilts", parent=parent)
    for x in (-1.4, -0.84):
        for y in (-1.45, -0.75):
            stilts.cylinder(0.05, 0.6, (x, y, 0.36), "old_wood", segments=6)
    stilts.box((0.7, 1.25, 0.06), (-1.12, -1.12, 0.64), "fresh_log", bevel=0.01)  # the platform
    for k in range(4):
        stilts.box((0.08, 0.04, 0.06), (-1.12 + (k - 1.5) * 0.15, -1.86, 0.17 + k * 0.12), "old_wood", bevel=0.005)  # ladder rungs
    rod(stilts, (-1.38, -1.86, 0.1), (-1.38, -1.84, 0.62), 0.02, "old_wood")
    rod(stilts, (-0.86, -1.86, 0.1), (-0.86, -1.84, 0.62), 0.02, "old_wood")
    stilts.build()

    car = Part("TimeMachine", pivot=nose, parent=parent)
    car_body(car, nose, s, wheels=False, door=False, bonnet=False, coil=True)
    for x in (-0.17, 0.17):  # the headlights, wired to the dynamo, burn as sacred lamps
        car.sphere(0.07 * s, (nose[0] + x * s, nose[1] - 0.02, nose[2] + 0.25 * s), "window", segments=8, rings=5, smooth=True)
    car_obj = car.build()

    hoses = Part("Hoses", parent=parent)
    rod(hoses, (-0.95, -1.78, 0.78), (-0.2, -1.95, 0.5), 0.035, "umbrella_black")
    rod(hoses, (-0.2, -1.95, 0.5), (0.45, -1.55, 0.22), 0.035, "umbrella_black")
    rod(hoses, (-1.25, -1.78, 0.75), (-0.3, -1.88, 0.3), 0.03, "gold_trim")
    rod(hoses, (-0.3, -1.88, 0.3), (0.38, -1.2, 0.2), 0.03, "gold_trim")
    hoses.build()
    return car_obj


def camp_comfort(part, roofed=True):
    """Hearth 2: what the cult made of the car, and the first copies of the relics."""
    # The other wheel, stood up at the front as a sign, with a woad-painted board.
    part.cylinder(0.22, 0.09, (-0.62, -1.78, 0.36), "umbrella_black", segments=12, rotation=(90, 0, 0))
    part.cylinder(0.1, 0.095, (-0.62, -1.78, 0.36), "chrome", segments=10, rotation=(90, 0, 0))
    rod(part, (-0.62, -1.7, 0.14), (-0.62, -1.7, 0.95), 0.025, "old_wood")
    part.box((0.44, 0.04, 0.16), (-0.62, -1.73, 0.86), "woad", bevel=0.01, rotation=(0, 4, 0))

    # The engine block on a column stump: the altar, with candles.
    part.cylinder(0.17, 0.45, (0.3, 1.18, 0.37), "roman_marble", segments=10)
    part.box((0.36, 0.26, 0.22), (0.3, 1.18, 0.71), "mail", bevel=0.02)
    for x in (0.2, 0.3, 0.4):
        part.cylinder(0.03, 0.05, (x, 1.18, 0.84), "fieldstone", segments=6)  # the cylinders, now sacred
    for x, y in ((0.06, 1.05), (0.54, 1.05), (0.12, 1.32), (0.5, 1.32)):
        part.cylinder(0.022, 0.12, (x, y, 0.64), "linen", segments=6)
        part.cylinder(0.018, 0.05, (x, y, 0.725), "fire", segments=5, radius_top=0.0)

    # A wing mirror on a pole, watching the road: the Eye.
    rod(part, (-1.78, -1.25, 0.14), (-1.78, -1.25, 1.65), 0.025, "old_wood")
    part.box((0.18, 0.08, 0.13), (-1.78, -1.25, 1.7), "car_paint", bevel=0.02)
    part.box((0.15, 0.085, 0.1), (-1.78, -1.255, 1.7), "glass", bevel=0.01)

    # Wattle in the gaps of the old walls.
    wattle_fence(part, (-1.68, -1.0), (-1.68, -0.3), height=0.45, z0=0.14)

    # The log banya, bolted onto the back-right corner, door facing the fire.
    for start, end in (((0.62, 0.55), (1.72, 0.55)), ((1.72, 0.55), (1.72, 1.72)), ((1.72, 1.72), (0.62, 1.72)), ((0.62, 1.72), (0.62, 0.55))):
        log_wall(part, start, end, height=0.72, z0=0.14, radius=0.06)
    if roofed:  # Hearth 3 puts a storey on top instead
        thatch_roof(part, 1.15, 1.2, (1.17, 1.13, 0.86), 0.48, lean=-2)
    part.box((0.22, 0.22, 0.9 if roofed else 1.75), (1.55, 1.5, 1.0 if roofed else 1.43), "fieldstone", bevel=0.02, rotation=(0, 2, 0))  # chimney
    part.box((0.24, 0.05, 0.42), (0.95, 0.52, 0.36), "tarred_wood", bevel=0.01)  # door
    part.box((0.14, 0.04, 0.09), (1.42, 0.52, 0.6), "window", bevel=0.005)

    # The first copies: woad-blue hubcaps on the banya and a clay steering wheel on a post.
    for x in (0.72, 1.62):
        part.cylinder(0.09, 0.03, (x, 0.5, 0.48), "woad", segments=10, rotation=(90, 0, 0))
    rod(part, (-1.3, 0.85, 0.14), (-1.3, 0.85, 0.82), 0.025, "old_wood")
    for k in range(10):
        a = 2 * math.pi * k / 10
        part.box((0.075, 0.03, 0.035), (-1.3 + 0.13 * math.cos(a), 0.83, 0.95 + 0.13 * math.sin(a)), "ochre_clay", bevel=0.005,
                 rotation=(0, -(math.degrees(a) + 90), 0))
    part.box((0.26, 0.03, 0.035), (-1.3, 0.83, 0.95), "ochre_clay", bevel=0.005)


def temple(part):
    """Hearth 3: a thatched upper floor on the banya, a water wheel, banners."""
    part.box((1.24, 1.3, 0.08), (1.17, 1.13, 0.9), "fresh_log", bevel=0.01)  # the new floor, overhanging the logs
    part.box((1.14, 1.2, 0.55), (1.17, 1.13, 1.22), "daub", bevel=0.03, rotation=(0, 0, -1.5))
    for x in (0.6, 1.74):
        part.box((0.07, 1.22, 0.56), (x, 1.13, 1.22), "old_wood", bevel=0.01)
    part.box((1.16, 0.07, 0.07), (1.17, 0.52, 1.47), "old_wood", bevel=0.01)
    part.box((0.16, 0.04, 0.16), (0.9, 0.52, 1.24), "window", bevel=0.005)
    part.box((0.16, 0.04, 0.16), (1.42, 0.52, 1.24), "window", bevel=0.005)
    thatch_roof(part, 1.2, 1.26, (1.17, 1.13, 1.5), 0.6, lean=-2)

    # A water wheel on the right, a wooden channel to the pool.
    wheel(part, (1.55, 0.12, 0.5), 0.36, spokes=6, rim=10)
    part.box((0.18, 0.5, 0.08), (1.45, -0.2, 0.22), "old_wood", bevel=0.01)

    # Banners on the columns, woad and madder.
    for x, colour in ((-1.6, "madder"), (-1.12, "woad")):
        part.box((0.24, 0.02, 0.62), (x, 1.37, 1.7), colour, bevel=0.005)
        part.box((0.26, 0.03, 0.04), (x, 1.37, 2.02), "old_wood", bevel=0.005)


def baths(part):
    """Hearth 4, the Comfort Era: a cistern on a tower, copper pipes to the pool, a sauna hut, the Duck Ascendant."""
    # The cistern tower at the back left: four posts, a hooped barrel, a pipe down to the pool's edge.
    for x, y in ((-1.62, 1.0), (-1.18, 1.0), (-1.62, 1.44), (-1.18, 1.44)):
        part.box((0.08, 0.08, 1.5), (x, y, 0.75), "old_wood", bevel=0.01)
    part.box((0.6, 0.6, 0.06), (-1.4, 1.22, 1.52), "fresh_log", bevel=0.01)
    part.cylinder(0.3, 0.5, (-1.4, 1.22, 1.8), "old_wood", segments=12)
    for z in (1.62, 1.8, 1.98):
        part.cylinder(0.31, 0.03, (-1.4, 1.22, z), "mail", segments=12)
    part.cylinder(0.27, 0.02, (-1.4, 1.22, 2.05), "water", segments=12)
    rod(part, (-1.18, 1.0, 1.55), (-0.2, -0.55, 0.32), 0.04, "gold_trim")          # copper pipe to the pool
    rod(part, (-0.2, -0.55, 0.32), (0.55, -0.9, 0.3), 0.04, "gold_trim")
    # The sauna hut, back right of the temple: logs, a turf roof, a little chimney and a bench by the door.
    part.box((0.7, 0.6, 0.5), (1.95, 1.75, 0.25), "fresh_log", bevel=0.03)
    part.box((0.78, 0.68, 0.08), (1.95, 1.75, 0.54), "grass", bevel=0.02, rotation=(0, 0, 3))
    part.box((0.12, 0.12, 0.3), (2.15, 1.85, 0.72), "fieldstone", bevel=0.01)
    part.box((0.18, 0.04, 0.3), (1.95, 1.43, 0.17), "tarred_wood", bevel=0.005)    # the door
    part.box((0.5, 0.14, 0.05), (1.6, 1.36, 0.2), "old_wood", bevel=0.005)          # bench
    # The Duck Ascendant: a carved duck, weld-painted, on a post above the temple roof.
    part.box((0.08, 0.08, 0.7), (1.17, 1.13, 1.95), "old_wood", bevel=0.01)
    rubber_duck(part, (1.17, 1.13, 2.35), size=2.4, yaw=-20, relic=False)


def build_hearth(hearth_root, level):
    """The whole Hearth at `level` (1–4) under `hearth_root`, with its animated `Fire` and `Steam`."""
    ruin_part = Part("Ruin", parent=hearth_root)
    ruin(ruin_part, level)
    camp(ruin_part, level)
    if level >= 2:
        camp_comfort(ruin_part, roofed=level < 3)
    if level >= 3:
        temple(ruin_part)
    if level >= 4:
        baths(ruin_part)
    ruin_obj = ruin_part.build()
    landed_car(ruin_obj, level)

    fx, fy = FIRE
    base = 0.32 if level >= 2 else 0.22
    (Part("Fire", pivot=(fx, fy, base), parent=ruin_obj)
     .cylinder(0.15, 0.42, (fx, fy, base + 0.2), "fire", segments=7, radius_top=0.0, smooth=True)
     .cylinder(0.1, 0.3, (fx + 0.07, fy + 0.04, base + 0.14), "ember", segments=6, radius_top=0.0, smooth=True, rotation=(8, 10, 0))
     .cylinder(0.08, 0.26, (fx - 0.08, fy - 0.03, base + 0.12), "ember", segments=6, radius_top=0.0, smooth=True, rotation=(-8, -12, 0))
     .build())

    # L1–2: the radiator steams where the nose went into the pool. L3: the hot pool itself.
    sx, sy, sz = (1.0, -1.05, 0.25) if level >= 3 else (1.05, -1.5, 0.35)
    steam = Part("Steam", pivot=(sx, sy, sz), parent=ruin_obj)
    puffs = ((0, 0, 0.12, 0.14), (0.1, 0.05, 0.32, 0.1), (-0.05, 0.1, 0.5, 0.07))
    if level >= 3:
        puffs += ((-0.4, 0.25, 0.15, 0.09), (0.35, -0.3, 0.22, 0.08), (-0.25, -0.2, 0.42, 0.06))
    for dx, dy, dz, r in puffs:
        steam.sphere(r, (sx + dx, sy + dy, sz + dz), "linen", segments=7, rings=5, smooth=True)
    steam.build()
