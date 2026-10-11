import unittest

from lib import checks, scene
from models import levy, manatarms, zealot


class EnemyModelTests(unittest.TestCase):
    def test_each_enemy_builds_within_budget_with_its_parts(self):
        for module in (levy, manatarms, zealot):
            with self.subTest(module.__name__):
                # Arrange
                scene.reset()

                # Act
                root = module.build()

                # Assert
                checks.verify(root, module.TRIANGLE_BUDGET, module.REQUIRED_PARTS)
