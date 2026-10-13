"""The time machine (spec: The Boot): the Prophet's teal hatchback, a copper coil bolted to the roof, gaffer tape
where the engineering should be. Built nose at `origin`, running back along +Y, so a crash pose is one rotation
about the nose."""


def _at(origin, s, x, y, z):
    ox, oy, oz = origin
    return (ox + x * s, oy + y * s, oz + z * s)


def car_body(part, origin=(0, 0, 0), s=1.0):
    """Everything but the boot lid."""
    def at(x, y, z):
        return _at(origin, s, x, y, z)

    part.box((0.54 * s, 1.0 * s, 0.24 * s), at(0, 0.5, 0.2), "car_paint", bevel=0.05 * s, bevel_segments=2)
    part.box((0.48 * s, 0.56 * s, 0.22 * s), at(0, 0.62, 0.41), "car_paint", bevel=0.05 * s, bevel_segments=2)
    part.box((0.42 * s, 0.02 * s, 0.17 * s), at(0, 0.33, 0.41), "glass", bevel=0.01 * s, rotation=(-32, 0, 0))   # windscreen
    part.box((0.49 * s, 0.38 * s, 0.11 * s), at(0, 0.64, 0.44), "glass", bevel=0.01 * s)                         # side windows
    for x in (-0.27, 0.27):
        for y in (0.2, 0.8):
            part.cylinder(0.11 * s, 0.08 * s, at(x, y, 0.11), "umbrella_black", segments=10, rotation=(0, 90, 0))
            part.cylinder(0.05 * s, 0.085 * s, at(x, y, 0.11), "chrome", segments=8, rotation=(0, 90, 0))
    for x in (-0.17, 0.17):
        part.box((0.1 * s, 0.02 * s, 0.06 * s), at(x, -0.005, 0.25), "window", bevel=0.01 * s)                   # headlights
    for y in (-0.01, 1.01):
        part.box((0.56 * s, 0.05 * s, 0.05 * s), at(0, y, 0.11), "chrome", bevel=0.015 * s)                      # bumpers
    part.box((0.18 * s, 0.012 * s, 0.05 * s), at(0, -0.04, 0.16), "linen", bevel=0.004 * s)                      # number plate
    for side in (-1, 1):                                                                                           # gaffer tape X
        part.box((0.012 * s, 0.26 * s, 0.03 * s), at(0.272, 0.48, 0.22), "chrome", bevel=0.002 * s, rotation=(side * 28, 0, 0))
    # The roof contraption: a mast, a copper coil, a red light that blinks for no reason.
    part.cylinder(0.02 * s, 0.2 * s, at(0, 0.62, 0.6), "gold_trim", segments=6)
    for k in range(3):
        part.cylinder((0.13 - k * 0.025) * s, 0.02 * s, at(0, 0.62, 0.62 + k * 0.045), "gold_trim", segments=12)
    part.sphere(0.03 * s, at(0, 0.62, 0.74), "laser_red", segments=8, rings=5, smooth=True)
    for x in (-0.12, 0.12):                                                                                        # cables to the bonnet
        part.cylinder(0.01 * s, 0.42 * s, at(x, 0.3, 0.47), "tarred_wood", segments=5, rotation=(62, 0, 0))


def boot_lid(part, origin=(0, 0, 0), s=1.0):
    """The hatch, hinged at the top of the tail (its pivot: `boot_hinge`)."""
    ox, oy, oz = origin
    part.box((0.46 * s, 0.03 * s, 0.32 * s), (ox, oy + 1.0 * s, oz + 0.36 * s), "car_paint", bevel=0.02 * s)
    part.box((0.38 * s, 0.032 * s, 0.13 * s), (ox, oy + 1.0 * s, oz + 0.44 * s), "glass", bevel=0.01 * s)


def boot_hinge(origin=(0, 0, 0), s=1.0):
    ox, oy, oz = origin
    return (ox, oy + 0.99 * s, oz + 0.52 * s)


def crashed_car(parent, nose=(0.14, 0.0, 0.16), s=0.68):
    """The landing, frozen forever: nose-first into the Roman pool (which is why it steams), tail in the air, roof
    and coil tipped towards the camera, boot hanging open (it has been unloaded). Returns the car object."""
    from lib.mesh import Part

    car = Part("TimeMachine", pivot=nose, parent=parent)
    car_body(car, nose, s)
    car_obj = car.build(rotation=(52, 0, 8))
    boot = Part("BootOpen", pivot=boot_hinge(nose, s), parent=car_obj)
    boot_lid(boot, nose, s)
    boot.build(rotation=(-70, 0, 0))
    return car_obj
