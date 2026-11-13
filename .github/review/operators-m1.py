#!/usr/bin/env python3
"""Run the same checked M1 driver as the integration regression, retaining evidence."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).resolve().parents[2] /
    'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194.py'),
    run_name='__main__')
