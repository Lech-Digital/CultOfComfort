"""Runs every art/blender/tests/test_*.py inside Blender: ./art/build.sh --test"""
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
BLENDER_DIR = os.path.dirname(HERE)
sys.path.insert(0, BLENDER_DIR)

suite = unittest.defaultTestLoader.discover(HERE, pattern="test_*.py", top_level_dir=BLENDER_DIR)
result = unittest.TextTestRunner(verbosity=2).run(suite)
sys.exit(0 if result.wasSuccessful() else 1)
