import importlib
import os
import unittest

from lib import checks, scene

MODELS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "models")


class AllModelsTests(unittest.TestCase):
    def test_every_model_builds_within_its_budget_with_its_parts(self):
        names = sorted(f[:-3] for f in os.listdir(MODELS) if f.endswith(".py") and not f.startswith("_"))
        self.assertGreater(len(names), 0)
        for name in names:
            with self.subTest(name):
                # Arrange
                scene.reset()
                module = importlib.import_module(f"models.{name}")

                # Act
                root = module.build()

                # Assert
                checks.verify(root, module.TRIANGLE_BUDGET, module.REQUIRED_PARTS)
