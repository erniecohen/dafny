# This scratch revision uses the existing compiler-only build entry point to
# build caller placement instrumentation. No product or solver gate is enabled.
from pathlib import Path
p=Path.cwd()/'.github/review/native-caller-placement-diagnostic/enable.py'
exec(compile(p.read_text(),str(p),'exec'),{'__file__':str(p),'__name__':'__main__'})
