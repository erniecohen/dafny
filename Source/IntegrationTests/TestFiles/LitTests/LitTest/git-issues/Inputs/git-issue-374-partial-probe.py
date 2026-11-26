"""Add term probes without constraining any symbol of the original prelude.

For every interpretation of Apply1, interpreting PartialProbe as true satisfies
both added axioms. This is a conservative extension, used to make applications
outside a precondition visible to the solver in the live vacuity controls.
"""
from pathlib import Path
import sys

prelude, output = map(Path, sys.argv[1:])
text = prelude.read_text()
text += '\nfunction PartialProbe(x: Box): bool;\n'
for value in (-1, 1):
    text += f'''axiom (forall t0: Ty, t1: Ty, h: Heap, f: HandleType, x: Box ::
  {{ Apply1(t0, t1, h, f, x) }}
  PartialProbe(Apply1(t0, t1, h, f, $Box({value}))));
'''
output.write_text(text)
