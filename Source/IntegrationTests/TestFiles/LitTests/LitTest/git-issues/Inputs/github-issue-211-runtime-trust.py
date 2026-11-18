"""Separate runtime trust: explicit, exact, named, and never a body exemption."""
from pathlib import Path
import shutil
import subprocess
import sys

assembly = Path(sys.argv[1]).resolve()
solver = str(Path(sys.argv[2]).resolve())
output = Path(sys.argv[3]).resolve()
if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
root = next(p for p in Path(__file__).resolve().parents if (p / 'Source/DafnyStandardLibraries').is_dir())
source = (root / 'Source/DafnyStandardLibraries/src/Std/TargetSpecific/FileIO3InternalExterns-notarget-java-cs-js-go-py.dfy').read_text()
option = '--modular-termination-runtime-trust:standard-library'
contract = 'Std.FileIOInternalExterns.INTERNAL_ReadBytesFromFile'


def run(name, command, files, refresh, trusted=False):
    args = ['dotnet', str(assembly), command, '--modular-termination',
            '--standard-libraries:false', '--solver-path', solver,
            '--type-system-refresh:' + str(refresh).lower(), '--use-basename-for-filename']
    if command != 'audit':
        args += ['--cores', '1', '--resource-limit', '1000000']
    if trusted:
        args.append(option)
    result = subprocess.run(args + files, capture_output=True, text=True)
    text = result.stdout + result.stderr
    (output / (name + '.log')).write_text(text)
    return result.returncode, text


for refresh in [False, True]:
    suffix = 'refresh' if refresh else 'legacy'
    library = output / (suffix + '.doo')
    original = output / (suffix + '.dfy')
    original.write_text(source)
    args = ['--target:lib', '--output:' + str(library), str(original)]
    code, text = run('producer-no-opt-in-' + suffix, 'build', args, refresh)
    assert code and option in text and not library.exists(), text
    print(suffix + ': producer requires separate runtime opt-in')
    code, text = run('producer-opt-in-' + suffix, 'build', args, refresh, True)
    assert code == 0 and library.exists() and contract in text and 'including termination' in text, text
    print(suffix + ': producer records and names runtime contracts')
    client = output / ('client-' + suffix + '.dfy')
    client.write_text('''module Client {
  import Std.FileIOInternalExterns
  method Use() {
    var failed, bytes, message := FileIOInternalExterns.INTERNAL_ReadBytesFromFile("unused");
    // CONTROL
  }
}
''')
    args = ['--library:' + str(library), str(client)]
    code, text = run('client-no-opt-in-' + suffix, 'verify', args, refresh)
    assert code and option in text, text
    print(suffix + ': client requires separate runtime opt-in')
    code, text = run('client-opt-in-' + suffix, 'verify', args, refresh, True)
    assert code == 0 and contract in text and 'including termination' in text, text
    print(suffix + ': opted-in client verifies and names assumptions')
    client.write_text(client.read_text().replace('// CONTROL', 'assert false;'))
    code, text = run('client-false-' + suffix, 'verify', args, refresh, True)
    assert code == 4 and text.count('Error: assertion might not hold') == 1 and '1 error' in text, text
    assert 'out of resource' not in text and 'timed out' not in text, text
    print(suffix + ': reachable false is the only failure')
    missing = output / ('missing-' + suffix + '.dfy')
    missing.write_text(source.replace('replaceable module Std.FileIOInternalExterns {',
      'replaceable module Std.FileIOInternalExterns {\n  method UnlistedRuntimeContract()'))
    missing_library = output / ('missing-' + suffix + '.doo')
    code, text = run('unlisted-' + suffix, 'build',
      ['--target:lib', '--output:' + str(missing_library), str(missing)], refresh, True)
    assert code and 'UnlistedRuntimeContract' in text and 'missing from the reviewed runtime list' in text, text
    assert not missing_library.exists(), text
    print(suffix + ': unlisted runtime contract remains rejected')
    for kind, altered in [
        ('statement', source.replace('returns (isError: bool, bytesRead: seq<bv8>, errorMsg: string)',
          'returns (isError: bool, bytesRead: seq<bv8>, errorMsg: string)\n    ensures isError || !isError')),
        ('context', source.replace('replaceable module Std.FileIOInternalExterns {',
          'replaceable module Std.FileIOInternalExterns {\n  const ChangedContext: int := 0'))]:
        changed = output / (kind + '-' + suffix + '.dfy')
        changed.write_text(altered)
        changed_library = output / (kind + '-' + suffix + '.doo')
        code, text = run(kind + '-' + suffix, 'build',
          ['--target:lib', '--output:' + str(changed_library), str(changed)], refresh, True)
        assert code and 'differs from its reviewed statement or context' in text, text
        assert not changed_library.exists(), text
        print(suffix + ': changed runtime ' + kind + ' remains rejected')
    code, text = run('audit-' + suffix, 'audit', ['--library:' + str(library), str(client)], refresh, True)
    assert code == 0 and contract in text and 'including termination' in text, text
    print(suffix + ': audit names the separate runtime assumptions')
