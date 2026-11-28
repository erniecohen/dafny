#!/usr/bin/env python3
"""Run the CommonJS ownership matrix with required acyclic ancestor owners."""
import runpy
from pathlib import Path

runpy.run_path(str(Path(__file__).with_name("recursive-module-js-require-refinements.py")))["main"](
    required_ancestors=True)
