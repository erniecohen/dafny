"""By-use certification and admission, including a forged manifest option."""
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zipfile

binary, solver, directory = [str(Path(p).resolve()) for p in sys.argv[1:]]
out = Path(directory)
if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)
for refresh in [False, True]:
    name = 'refresh' if refresh else 'legacy'
    common = ['--standard-libraries:false', '--solver-path', solver,
              '--type-system-refresh:' + str(refresh).lower(), '--cores', '1',
              '--resource-limit', '1000000', '--use-basename-for-filename']
    def run(case, command, files, expected=0, contains=()):
        result = subprocess.run(['dotnet', binary, command] + common + files,
                                capture_output=True, text=True)
        text = result.stdout + result.stderr
        (out / (name + '-' + case + '.log')).write_text(text)
        assert result.returncode == expected and all(p in text for p in contains), text
        assert not re.search(r'out of resource|timed out|Unhandled exception', text, re.I), text
        print(name + ': ' + case + ' PASS')
        return text
    source, client, library = [out / (name + suffix) for suffix in ['-source.dfy', '-client.dfy', '.doo']]
    source.write_text('module Library { function {:termination_closed} F(): int { 1 } }')
    run('producer', 'build', [str(source), '--target:lib', '--output:' + str(library)])
    with zipfile.ZipFile(library) as archive:
        assert 'termination-contracts.json' in archive.namelist()
    client.write_text('module Client { import Library method Use() { assert Library.F() == 1; } }')
    run('client', 'verify', [str(client), '--library:' + str(library)])
    run('dependent-producer', 'build', [str(client), '--library:' + str(library),
        '--target:lib', '--output:' + str(out / (name + '-dependent.doo'))])
    client.write_text('module Client { import Library method Use() { assert Library.F() == 1; assert false; } }')
    text = run('false', 'verify', [str(client), '--library:' + str(library)], 4,
               ['Error: assertion might not hold', '1 error'])
    assert text.count('Error: assertion might not hold') == 1, text
    run('legacy-consumer', 'verify', [str(client), '--library:' + str(library),
        '--legacy-no-modular-termination'], 1, ['checked only under that option'])
    unrecorded = out / (name + '-unrecorded.doo')
    run('legacy-producer', 'build', [str(source), '--target:lib', '--output:' + str(unrecorded),
        '--legacy-no-modular-termination'])
    forged = out / (name + '-forged.doo')
    with zipfile.ZipFile(unrecorded) as old, zipfile.ZipFile(forged, 'w') as new:
        assert 'termination-contracts.json' not in old.namelist()
        for entry in old.namelist():
            content = old.read(entry)
            if entry == 'manifest.toml':
                content = content.replace(b'legacy-no-modular-termination = true',
                                          b'legacy-no-modular-termination = false')
            new.writestr(entry, content)
    run('forged-option', 'verify', [str(client), '--library:' + str(forged)], 2,
        ['not verified under the rules of termination across modules'])
    source.write_text('module Ordinary { function F(): int { 1 } }')
    ordinary = out / (name + '-ordinary.doo')
    run('ordinary-producer', 'build', [str(source), '--target:lib', '--output:' + str(ordinary)])
    with zipfile.ZipFile(ordinary) as archive:
        assert 'termination-contracts.json' not in archive.namelist()
        assert b'legacy-no-modular-termination' not in archive.read('manifest.toml')
    client.write_text('module Client { import Ordinary method Use() { assert Ordinary.F() == 1; } }')
    run('ordinary-client', 'verify', [str(client), '--library:' + str(ordinary)])
    run('ordinary-legacy-client', 'verify', [str(client), '--library:' + str(ordinary),
        '--legacy-no-modular-termination'])
    # No two-argument function is parsed here: the intrinsic certify signature must
    # be initialized before builtin resolution, independently of user declarations.
    formation = ('function Leaf(x: int): int { x } '
                 'method Use() { var c: bounded<nat, (int -> int)> := bounded.certify(0, Leaf); '
                 'assert c(3) == 3; // CONTROL\n}')
    source.write_text(formation)
    run('bounded-formation', 'verify', [str(source)])
    source.write_text(formation.replace('// CONTROL', 'assert false;'))
    text = run('bounded-formation-false', 'verify', [str(source)], 4,
               ['Error: assertion might not hold', '1 error'])
    assert text.count('Error: assertion might not hold') == 1, text
    source.write_text('function Bad(c: bounded<ORDINAL, (int -> int)>, x: int): int '
                      'decreases {:global} c.bound { c(x) }')
    run('bounded-application', 'verify', [str(source)], 4, ['decreases clause might not decrease'])
    source.write_text('module Unchecked { lemma {:axiom} Bad() ensures false }')
    unchecked = out / (name + '-unchecked.doo')
    run('unchecked-ordinary-producer', 'build', [str(source), '--target:lib', '--output:' + str(unchecked)])
    client.write_text('module Client { import Unchecked const {:termination_closed} Enabled: bool := true '
                      'method Use() { Unchecked.Bad(); assert false; } }')
    result = out / (name + '-laundered.doo')
    run('unrecorded-dependency', 'build', [str(client), '--library:' + str(unchecked),
        '--target:lib', '--output:' + str(result)], 3, ['depends on unrecorded library'])
    assert not result.exists()

# The order-library autoloader requires the refreshed type system. Check the opt-out
# independently of library option compatibility, with an import that needs that loader.
source.write_text('module Client { import Std.Termination.Orders }')
run('order-autoload-default', 'verify', [str(source), '--decreases-orders', '--general-traits=datatype'])
run('order-autoload-legacy', 'verify', [str(source), '--decreases-orders', '--general-traits=datatype',
    '--legacy-no-modular-termination'], 2, ['Error:'])
