from pathlib import Path
import runpy
runpy.run_path(str(Path.cwd()/".github/review/native-terminal-exits-diagnostic/enable.py"),run_name="__main__")
