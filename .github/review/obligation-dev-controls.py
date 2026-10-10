"""Diagnostic replay of the unchanged registered oracle, retaining every outcome.

Assertions become recorded observations only in this diagnostic interpreter.
The registered test remains strict and continues to fail on its first finding.
"""
import ast, json, subprocess, sys, traceback
from pathlib import Path
assembly, solver, work = map(lambda x: Path(x).resolve(), sys.argv[1:4])
work.mkdir(parents=True, exist_ok=True)
source = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/assertion-invariance/registered-regression.py').resolve()
checks, invocations = [], []
def observe(test, message, line):
    checks.append({'line': line, 'passed': bool(test), 'message': '' if test else str(message)})
class DiagnosticAssertions(ast.NodeTransformer):
    def visit_Assert(self, node):
        return ast.copy_location(ast.Expr(ast.Call(ast.Name('observe', ast.Load()),
            [node.test, node.msg or ast.Constant('assertion failed'), ast.Constant(node.lineno)], [])), node)
class RecordedSubprocess:
    check_output = staticmethod(subprocess.check_output)
    def run(self, command, **kwargs):
        result = subprocess.run(command, **kwargs)
        if 'verify' in command and '--help' not in command:
            folder = work / ('invocation-%03d' % len(invocations)); folder.mkdir()
            (folder / 'command.json').write_text(json.dumps(command))
            (folder / 'output.txt').write_text((result.stdout or '') + (result.stderr or ''))
            invocations.append({'index':len(invocations),'source':command[3], 'exit':result.returncode,
                'enabled':next((x for x in command if x.startswith('--consistent-obligation-checks')), 'project'),
                'refresh':next((x for x in command if x.startswith('--type-system-refresh')), ''),
                'isolate':'--isolate-assertions' in command})
        return result
scope = {'__name__':'__diagnostic__', '__file__':str(source), 'observe':observe,
    'print':lambda *items, **kwargs: print('Diagnostic checkpoint:', *(str(x).removeprefix('PASS ') for x in items), **kwargs)}
tree = ast.parse(source.read_text())
# Use the recording facade without altering the imported module globally.
tree.body = [node for node in tree.body if not (isinstance(node,ast.Import) and any(a.name=='subprocess' for a in node.names))]
scope['subprocess'] = RecordedSubprocess()
saved = sys.argv; sys.argv=[str(source),str(assembly),str(solver),str(work)]
try:
    exec(compile(ast.fix_missing_locations(DiagnosticAssertions().visit(tree)),str(source),'exec'),scope)
except Exception:
    (work/'exception.txt').write_text(traceback.format_exc())
finally:
    sys.argv=saved
    result={'diagnostic':True,'source_oracle':str(source),'checks':checks,'invocations':invocations,
        'failed_checks':sum(not x['passed'] for x in checks),'invocation_count':len(invocations)}
    (work/'diagnostic.json').write_text(json.dumps(result,indent=2)+'\n')
    print('Diagnostic replay:',len(invocations),'invocations;',result['failed_checks'],'failed observations. Strict registered acceptance remains separate.')
