"""The time machine: the Prophet's teal hatchback, a copper coil on the roof, gaffer tape on the door. Parts for the
intro: `Car` (body), `Boot` (hinged at the top of the tail: rotate about X to open), `Coil` glows."""
from lib.car import boot_hinge, boot_lid, car_body
from lib.mesh import Part, marker, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Car", "Boot")


def build():
    machine = root("time_machine")
    origin = (0, -0.5, 0)  # centred on its own length
    car = Part("Car", parent=machine)
    car_body(car, origin)
    car_obj = car.build()
    boot = Part("Boot", pivot=boot_hinge(origin), parent=car_obj)
    boot_lid(boot, origin)
    boot.build()
    marker("Front", (0, -0.6, 0.2), machine)
    return machine
