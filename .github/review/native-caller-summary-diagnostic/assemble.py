from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).resolve().parents[1]/'native-caller-scope-diagnostic/assemble.py'),run_name='__main__')
