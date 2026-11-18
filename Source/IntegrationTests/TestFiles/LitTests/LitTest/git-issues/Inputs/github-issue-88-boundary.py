"""A library subtrait is allowed; recursive foreign implementations are not."""
from pathlib import Path
import re
import subprocess
import sys

binary, solver, library, directory = [str(Path(p).resolve()) for p in sys.argv[1:]]
out = Path(directory)
out.mkdir(parents=True, exist_ok=True)
trait = (Path(__file__).parent / 'github-issue-88-client-rejected.dfy').read_text()
leaf = '''module App {
  import LibTraits
  class C extends LibTraits.Named {
    constructor () {}
    function Name(): string { "leaf" }
  }
  method Exercise() {
    var c := new C();
    assert c.Length() == 4;
    // CONTROL
  }
}
'''
nonleaf = leaf.replace('"leaf"', 'if Length() == 0 then "" else "loop"')
for refresh in [False, True]:
    label = 'refresh' if refresh else 'legacy'
    options = ['--solver-path', solver, '--cores', '1', '--resource-limit', '16000000',
               '--refinement-extends', '--type-system-refresh:' + str(refresh).lower(),
               '--library', library, '--standard-libraries:false', '--use-basename-for-filename']
    for name, text, expected, required, extra in [
        ('subtrait', trait, 0, ['0 errors'], []),
        ('leaf', leaf, 0, ['0 errors'], []),
        ('false', leaf.replace('// CONTROL', 'assert false;'), 4,
         ['Error: assertion might not hold', '1 error'], []),
        ('nonleaf', nonleaf, 2, ["implements 'LibTraits.Named.Name'", 'but it is not a leaf'], []),
        ('legacy-opt-out', trait, 2, ["trait 'T' is in a different module"],
         ['--legacy-no-modular-termination']),
    ]:
        source = out / (label + '-' + name + '.dfy')
        source.write_text(text)
        result = subprocess.run(['dotnet', binary, 'verify'] + options + extra + [str(source)],
                                capture_output=True, text=True)
        output = result.stdout + result.stderr
        (out / (label + '-' + name + '.log')).write_text(output)
        assert result.returncode == expected and all(part in output for part in required), output
        assert not re.search(r'out of resource|timed out|Unhandled exception', output, re.I), output
        if name == 'false':
            assert output.count('Error: assertion might not hold') == 1, output
        print(label + ': ' + name + ' PASS')
