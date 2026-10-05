"""Source-only 35-control unsigned If guard scratch receipt.

Expected failures exit zero after bounded owned cleanup and remain NOT GREEN.
A passing receipt qualifies only fresh current-source Core/test compilation and
these 35 controls. It proves no library, worker, corpus or default parity claim.
"""
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import selectors
import shutil
import signal
import stat
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path.cwd().resolve()
INPUTS_PATH = Path('.github/review/b3-if-guard-focus-inputs.json')
FILTER = ('FullyQualifiedName~DafnyB3Normalizer.Test.B3IfGuardCertificateTests|'
          'FullyQualifiedName=DafnyB3Normalizer.Test.B3UnsignedWrapperTests.'
          'ActualDafnyDirectRangeRoundTripAndFalseChecksRemainPresent')
MAXIMUM_FILE_BYTES = 512 * 1024 * 1024
MAXIMUM_INVENTORY_BYTES = 4 * 1024 * 1024 * 1024
MAXIMUM_INVENTORY_FILES = 40000
MAXIMUM_ARTIFACT_BYTES = 256 * 1024 * 1024
TRX_NS = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'


def digest_bytes(data):
    return hashlib.sha256(data).hexdigest()


def seal(value):
    return digest_bytes(json.dumps(value, sort_keys=True, separators=(',', ':')).encode())


def file_record(path):
    info = path.lstat()
    assert stat.S_ISREG(info.st_mode), 'Require a regular inventory file: ' + str(path)
    assert info.st_size <= MAXIMUM_FILE_BYTES, 'File exceeds inventory bound: ' + str(path)
    digest = hashlib.sha256()
    length = 0
    with path.open('rb') as stream:
        while chunk := stream.read(1024 * 1024):
            length += len(chunk)
            assert length <= MAXIMUM_FILE_BYTES, 'File grew past inventory bound'
            digest.update(chunk)
    assert length == info.st_size, 'Inventory file length changed during hashing'
    return {'bytes': length, 'sha256': digest.hexdigest()}


def directory_records(root):
    assert root.is_dir() and not root.is_symlink(), 'Require a real inventory directory'
    rows = []
    total = 0
    pending = [root]
    while pending:
        directory = pending.pop()
        for path in sorted(directory.iterdir()):
            assert not path.is_symlink(), 'Directory inventory does not follow symlinks'
            if path.is_dir():
                pending.append(path)
            else:
                record = file_record(path)
                total += record['bytes']
                assert total <= MAXIMUM_INVENTORY_BYTES, 'Inventory byte bound exceeded'
                rows.append({'path': path.relative_to(root).as_posix(), **record})
                assert len(rows) <= MAXIMUM_INVENTORY_FILES, 'Inventory file bound exceeded'
    return sorted(rows, key=lambda row: row['path'])


def git_bytes(*args):
    # Metadata tools are bounded and run only in this dedicated owned scope.
    assert not direct_children(), 'Require empty child scope before Git metadata'
    result = subprocess.run(['git', *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            timeout=30, check=True)
    assert not direct_children(), 'Git metadata left an owned child'
    assert len(result.stdout) <= 64 * 1024 * 1024, 'Git metadata byte bound exceeded'
    return result.stdout


def tree_records(revision):
    rows = {}
    for item in git_bytes('ls-tree', '-r', '-z', revision).split(b'\0'):
        if not item:
            continue
        metadata, path_bytes = item.split(b'\t', 1)
        mode, kind, blob = metadata.decode().split()
        path = path_bytes.decode('utf-8')
        assert kind == 'blob' and mode in {'100644', '100755', '120000'}, 'Unexpected source tree entry'
        assert path not in rows and not path.startswith('/') and '..' not in Path(path).parts
        rows[path] = {'mode': mode, 'gitBlob': blob}
    assert len(rows) <= MAXIMUM_INVENTORY_FILES
    return rows


def worktree_records(tree):
    rows = []
    total = 0
    for name, entry in sorted(tree.items()):
        path = Path(name)
        # Never follow a symlink ancestor for tracked file contents.
        assert all(not ancestor.is_symlink() for ancestor in path.parents), 'Source symlink ancestor'
        if entry['mode'] == '120000':
            assert path.is_symlink(), 'Tracked symlink changed kind'
            data = os.readlink(path).encode('utf-8')
        else:
            assert path.is_file() and not path.is_symlink(), 'Tracked regular file changed kind'
            info = path.stat()
            assert info.st_size <= MAXIMUM_FILE_BYTES, 'Source file byte bound exceeded'
            assert bool(info.st_mode & stat.S_IXUSR) == (entry['mode'] == '100755'), 'Tracked executable mode changed'
            git_digest = hashlib.sha1(b'blob ' + str(info.st_size).encode() + b'\0')
            digest = hashlib.sha256()
            length = 0
            with path.open('rb') as stream:
                while chunk := stream.read(1024 * 1024):
                    length += len(chunk)
                    assert length <= MAXIMUM_FILE_BYTES, 'Tracked source grew past byte bound'
                    git_digest.update(chunk)
                    digest.update(chunk)
            assert length == info.st_size, 'Tracked source length changed during hashing'
            blob, sha256 = git_digest.hexdigest(), digest.hexdigest()
        if entry['mode'] == '120000':
            length = len(data)
            blob = hashlib.sha1(b'blob ' + str(length).encode() + b'\0' + data).hexdigest()
            sha256 = digest_bytes(data)
        total += length
        assert total <= MAXIMUM_INVENTORY_BYTES
        untouched = {row['path']: row for row in inputs['untouchedCheckoutFiles']}
        if name in untouched:
            frozen = untouched[name]
            assert entry['mode'] == frozen['mode'] and entry['gitBlob'] == frozen['gitBlob']
            assert length == frozen['checkoutBytes'] and sha256 == frozen['checkoutSha256'], 'Untouched checkout bytes changed'
            byte_kind = 'frozen-nonselected-CRLF-checkout'
        else:
            assert blob == entry['gitBlob'], 'Tracked source differs from committed blob: ' + name
            byte_kind = 'exact-committed-Git-blob'
        rows.append({'path': name, **entry, 'byteKind': byte_kind, 'bytes': length, 'sha256': sha256})
    return rows


def prepare_checkout_bytes(tree):
    declared = inputs['checkoutMaterializations']
    untouched = inputs['untouchedCheckoutFiles']
    assert len(declared) == 43 and len(untouched) == 12
    assert len({row['path'] for row in declared + untouched}) == 55
    assert all(Path(row['path']).suffix in {'.csproj', '.config'} for row in declared)
    assert all(Path(row['path']).suffix in {'.sln', '.bat', '.transcript'} and not row['selectedByThisFocus'] for row in untouched)
    assert not Path('.git/info/attributes').exists(), 'No hidden checkout attribute override'
    raw_attributes = git_bytes('check-attr', '-z', 'eol', '--', *(row['path'] for row in declared + untouched)).split(b'\0')
    assert raw_attributes[-1] == b''
    attrs = {raw_attributes[index].decode(): (raw_attributes[index + 1].decode(), raw_attributes[index + 2].decode())
        for index in range(0, len(raw_attributes) - 1, 3)}
    assert len(attrs) == 55 and all(value == ('eol', 'crlf') for value in attrs.values())
    receipts = []
    for row in declared + untouched:
        path = Path(row['path'])
        assert tree[row['path']] == {'mode': row['mode'], 'gitBlob': row['gitBlob']}
        assert row['mode'] in {'100644', '100755'} and all(not ancestor.is_symlink() for ancestor in path.parents)
        before = file_record(path)
        raw = path.read_bytes()
        assert len(raw) <= MAXIMUM_FILE_BYTES and len(raw) == before['bytes']
        if row in untouched:
            assert before == {'bytes': row['checkoutBytes'], 'sha256': row['checkoutSha256']}
            canonical = raw.replace(b'\r\n', b'\n')
            disposition = 'untouched-nonselected-exact-checkout-bytes'
        else:
            assert before in [{'bytes': row['checkoutBytes'], 'sha256': row['checkoutSha256']},
                              {'bytes': row['gitBytes'], 'sha256': row['gitSha256']}]
            canonical = raw.replace(b'\r\n', b'\n') if before['sha256'] == row['checkoutSha256'] else raw
            assert len(canonical) == row['gitBytes'] and digest_bytes(canonical) == row['gitSha256']
            assert hashlib.sha1(b'blob ' + str(len(canonical)).encode() + b'\0' + canonical).hexdigest() == row['gitBlob']
            if raw != canonical:
                path.write_bytes(canonical)
            disposition = 'materialized-exact-Git-blob' if raw != canonical else 'already-exact-Git-blob'
        assert len(canonical) == row['gitBytes'] and digest_bytes(canonical) == row['gitSha256']
        assert hashlib.sha1(b'blob ' + str(len(canonical)).encode() + b'\0' + canonical).hexdigest() == row['gitBlob']
        after = file_record(path)
        assert (path.stat().st_mode & stat.S_IXUSR != 0) == (row['mode'] == '100755')
        receipts.append({'path': row['path'], 'mode': row['mode'], 'gitBlob': row['gitBlob'],
            'attribute': attrs[row['path']][1], 'selectedByThisFocus': row['selectedByThisFocus'],
            'gitBytes': row['gitBytes'], 'gitSha256': row['gitSha256'], 'before': before,
            'after': after, 'disposition': disposition})
    return receipts


def execution_controls_from_source():
    result = []
    for control in inputs['controls']:
        source = Path(inputs['guardTestSource'] if control['className'] == inputs['guardClass'] else inputs['originalTestSource']).read_text()
        signature = re.search(r'public (?:void|async Task) ' + re.escape(control['method']) + r'\(([^)]*)\)', source)
        assert signature is not None
        parameters = signature[1].split(',') if signature[1].strip() else []
        names = []
        for parameter in parameters:
            parts = parameter.split()
            assert len(parts) == 2 and parts[0] in {'string', 'int'}
            names.append(parts[1])
        arguments = [json.loads('[' + data + ']') for data in control['inlineDataSource']] if parameters else [[]]
        assert len(arguments) == control['count']
        for values in arguments:
            assert len(values) == len(names)
            rendered = []
            for name, value in zip(names, values):
                if isinstance(value, str):
                    # This frozen fragment has only printable ASCII; the pinned
                    # xUnit EscapeHexChars pass is therefore the identity.
                    assert all(32 <= ord(char) <= 126 for char in value)
                    escaped = value.replace('"', '\\"')
                    formatted = '"' + escaped[:50] + '"' + ('...' if len(escaped) > 50 else '')
                else:
                    assert type(value) is int
                    formatted = str(value)
                rendered.append(name + ': ' + formatted)
            display = control['className'] + '.' + control['method']
            if rendered:
                display += '(' + ', '.join(rendered) + ')'
            result.append({'className': control['className'], 'method': control['method'],
                'parameterNames': names, 'arguments': values, 'displayName': display})
    assert len(result) == len({row['displayName'] for row in result}) == 35
    return result


def extract_controls(path, class_name, only=None):
    text = path.read_text()
    pattern = r'((?:^  \[(?:Fact|Theory|InlineData)[^\n]*\]\n)+)  public (?:async Task|void) (\w+)\('
    rows = []
    for match in re.finditer(pattern, text, re.M):
        method = match[2]
        if only is not None and method != only:
            continue
        attributes = match[1].strip().splitlines()
        data = [attribute.strip()[12:-2] for attribute in attributes
                if attribute.strip().startswith('[InlineData(')]
        rows.append({'className': class_name, 'method': method,
                     'attributes': [attribute.strip() for attribute in attributes],
                     'inlineDataSource': data, 'count': len(data) if data else 1})
    return rows


def fixture_record():
    source = Path('Source/DafnyB3Normalizer.Test/B3UnsignedWrapperTests.cs').read_text()
    section = source.split('public async Task ActualDafnyDirectRangeRoundTripAndFalseChecksRemainPresent()', 1)[1]
    literal = section.split('"""\n', 1)[1].split('\n      """', 1)[0]
    assert all(not line or line.startswith('      ') for line in literal.splitlines())
    data = '\n'.join(line[6:] for line in literal.splitlines()).encode()
    helper = Path('Source/DafnyB3Normalizer.Test/B3DefinitionContextTests.cs').read_text()
    assert 'new Uri("file:///B3VisibilityTests.dfy")' in helper
    assert 'options.ApplyDefaultOptionsWithoutSettingsDefault();' in helper
    assert 'Assert.Equal(3, results.Count);' in section
    assert 'Assert.Equal(new[] { 4, 1, 2 }, results.Select(result => result.Obligations.Count).ToArray());' in section
    return {'bytes': len(data), 'sha256': digest_bytes(data),
            'uri': 'file:///B3VisibilityTests.dfy',
            'options': 'ApplyDefaultOptionsWithoutSettingsDefault', 'units': 3, 'checks': [4, 1, 2]}


def validate_selection(inputs):
    assert os.environ.get('GITHUB_EVENT_NAME') == 'workflow_dispatch'
    assert os.environ.get('GITHUB_REF', '').startswith('refs/heads/scratch/')
    expected = {'b3_focus_gate': 'unsigned-if-guard', 'b3_compile_only': 'true',
                'b3_full_gate': 'false', 'binaries': 'false',
                'resolver_probe': 'false', 'additional_axioms_probe': 'false'}
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    actual = {key: str(event.get('inputs', {}).get(key, '')).lower() for key in expected}
    assert actual == expected, 'Require the exact dedicated focus dispatch inputs'
    for key, value in expected.items():
        assert os.environ.get('B3_INPUT_' + key.upper(), '').lower() == value, 'Workflow/coordinator input differs'
    return {'event': 'workflow_dispatch', 'ref': os.environ['GITHUB_REF'], 'inputs': actual,
            'eventFile': file_record(Path(os.environ['GITHUB_EVENT_PATH']))}


def capture_toolchain():
    lexical = Path(shutil.which('dotnet') or '')
    assert lexical.is_file(), 'dotnet executable is absent'
    executable = lexical.resolve(strict=True)
    root = executable.parent
    roots = [root / 'host' / 'fxr']
    roots.extend(sorted(path for path in (root / 'sdk').iterdir() if path.name.startswith('8.') and path.is_dir()))
    for family in ['Microsoft.NETCore.App', 'Microsoft.AspNetCore.App']:
        parent = root / 'shared' / family
        if parent.exists():
            roots.extend(sorted(path for path in parent.iterdir() if path.name.startswith('8.') and path.is_dir()))
    assert any(path.parent.name == 'sdk' for path in roots), 'Require installed .NET 8 SDK'
    assert any(path.parent.name == 'Microsoft.NETCore.App' for path in roots), 'Require installed .NET 8 runtime'
    directories = []
    total = 0
    count = 0
    for path in roots:
        rows = directory_records(path)
        total += sum(row['bytes'] for row in rows)
        count += len(rows)
        assert total <= MAXIMUM_INVENTORY_BYTES and count <= MAXIMUM_INVENTORY_FILES
        directories.append({'path': str(path), 'files': rows, 'sealSha256': seal(rows)})
    # Resolve the chosen executable and bind its ancestor identities as well.
    links = []
    for path in [lexical, *lexical.parents]:
        if path.is_symlink():
            links.append({'path': str(path), 'link': os.readlink(path), 'resolved': str(path.resolve(strict=True))})
    return {'executable': {'lexicalPath': str(lexical), 'resolvedPath': str(executable), **file_record(executable)},
            'ancestorSymlinks': links, 'directories': directories, 'totalBytes': total, 'fileCount': count}


def validate_source_initial():
    global head, source_tree, source_rows, toolchain_rows
    assert git_bytes('status', '--porcelain', '--untracked-files=all') == b'', 'Require clean initial checkout'
    head = git_bytes('rev-parse', 'HEAD').decode().strip()
    assert re.fullmatch('[0-9a-f]{40}', head) and head == os.environ.get('GITHUB_SHA')
    git_bytes('merge-base', '--is-ancestor', inputs['implementationHead'], head)
    assert git_bytes('rev-parse', inputs['implementationHead'] + '^{tree}').decode().strip() == inputs['implementationTree']
    assert Path('.github/review/base').read_text() == 'v4.11.0 ' + inputs['productCommit'] + '\n'
    # These exclusions match version.sh literally.
    exclusions = [':(exclude)' + path for path in ['Source/IntegrationTests', 'docs', '.github',
                                                  'REVIEW.md', 'ROADMAP.md', 'AGENTS.md', 'CLAUDE.md']]
    assert git_bytes('diff', '--name-only', inputs['productCommit'], 'HEAD', '--', '.', *exclusions) == b''
    source_tree = tree_records('HEAD')
    parent_tree = tree_records(inputs['implementationHead'])
    changed = {path for path in source_tree.keys() | parent_tree.keys()
               if source_tree.get(path) != parent_tree.get(path)}
    assert changed == set(inputs['allowedRoutingFiles']), 'Routing child changed an unexpected frozen file'
    source_receipt['checkoutBytePreparation'] = prepare_checkout_bytes(source_tree)
    source_rows = worktree_records(source_tree)
    by_path = {row['path']: row for row in source_rows}
    assert seal(inputs['implementationFiles']) == inputs['implementationFilesSealSha256']
    for row in inputs['implementationFiles'] + inputs['pinnedInputs']:
        actual = by_path[row['path']]
        assert {key: actual[key] for key in row} == row, 'Frozen implementation/input bytes differ'
    actual_controls = extract_controls(Path(inputs['guardTestSource']), inputs['guardClass'])
    actual_controls += extract_controls(Path(inputs['originalTestSource']), inputs['originalClass'], inputs['originalMethod'])
    assert actual_controls == inputs['controls'] and sum(row['count'] for row in actual_controls) == 35
    assert execution_controls_from_source() == inputs['expectedExecutionControls']
    assert fixture_record() == inputs['fixture'], 'Original 201-byte fixture/options/obligations changed'
    assert '.github/review/b3-native-compile.py' in by_path
    validate_copied_helper()
    toolchain_rows = capture_toolchain()
    (output / 'source-files.json').write_text(json.dumps(source_rows, indent=2) + '\n')
    (output / 'toolchain-files.json').write_text(json.dumps(toolchain_rows, indent=2) + '\n')
    source_receipt.update({'head': head, 'implementationHead': inputs['implementationHead'],
        'productCommit': inputs['productCommit'], 'implementationFilesSealSha256': inputs['implementationFilesSealSha256'],
        'trackedFileCount': len(source_rows), 'trackedFilesSealSha256': seal(source_rows),
        'routingFiles': [by_path[path] for path in inputs['allowedRoutingFiles']],
        'controlSealSha256': seal(inputs['controls']), 'executionControlSealSha256': seal(inputs['expectedExecutionControls']),
        'expectedExecutionControls': inputs['expectedExecutionControls'], 'displayNameSources': inputs['displayNameSources'],
        'fixture': fixture_record(),
        'sourceInventory': file_record(output / 'source-files.json'),
        'toolchainInventory': file_record(output / 'toolchain-files.json'),
        'toolchainSealSha256': seal(toolchain_rows), 'selectedDispatch': validate_selection(inputs)})


def validate_copied_helper():
    import ast
    original = Path('.github/review/b3-native-compile.py').read_text()
    current = Path(__file__).read_text()
    for name in inputs['copiedOwnershipFunctions']:
        def text_of(source):
            node = next(node for node in ast.parse(source).body if isinstance(node, ast.FunctionDef) and node.name == name)
            return ''.join(source.splitlines(keepends=True)[node.lineno - 1:node.end_lineno])
        assert text_of(original) == text_of(current), 'Reviewed ownership function changed: ' + name


def boundary(stage, when):
    assert not direct_children() and not cleanup_poisoned, 'Require empty unpoisoned child scope at boundary'
    assert git_bytes('rev-parse', 'HEAD').decode().strip() == head, 'HEAD changed across stage'
    rows = worktree_records(source_tree)
    assert rows == source_rows and fixture_record() == inputs['fixture'], 'Tracked source changed across stage'
    actual_tools = capture_toolchain()
    assert actual_tools == toolchain_rows, 'Actual dotnet SDK/runtime byte pins changed across stage'
    if package_receipt is not None:
        assert package_records() == package_receipt, 'Pinned package feed changed across stage'
    if core_rows is not None:
        assert directory_records(core_output) == core_rows, 'Current Core outputs changed across stage'
    if core_dependencies is not None:
        assert capture_core_dependency_inputs() == core_dependencies, 'Core SDK-resolved dependency bytes changed across stage'
    if java_runtime_jar is not None:
        assert file_record(Path('Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime-4.11.0.jar')) == java_runtime_jar
    if test_rows is not None:
        assert directory_records(test_output) == test_rows, 'Current test/config/dependency outputs changed across stage'
    receipt = {'stage': stage, 'boundary': when, 'trackedFilesSealSha256': seal(rows),
               'fixtureSha256': inputs['fixture']['sha256'], 'toolchainSealSha256': seal(actual_tools),
               'coreSealSha256': seal(core_rows) if core_rows is not None else None,
               'testSealSha256': seal(test_rows) if test_rows is not None else None}
    boundary_receipts.append(receipt)
    return receipt


def package_records():
    rows = []
    for line in Path('Scripts/boogie-packages.sha256').read_text().splitlines():
        digest, name = line.split()
        assert re.fullmatch('[0-9a-f]{64}', digest) and '/' not in name
        record = file_record(Path('Binaries/boogie-packages') / name)
        assert record['sha256'] == digest, 'Pinned Boogie package differs: ' + name
        rows.append({'path': name, **record})
    assert len(rows) == 14 and len({row['path'] for row in rows}) == 14
    return rows


def pinned_boogie_assembly_entries():
    expected = {}
    for package in package_receipt:
        with zipfile.ZipFile(Path('Binaries/boogie-packages') / package['path']) as archive:
            entries = archive.infolist()
            assert len(entries) <= 4096, 'Pinned package entry bound exceeded'
            for entry in entries:
                name = Path(entry.filename).name
                if not name.startswith('Boogie') or not name.endswith('.dll'):
                    continue
                assert entry.file_size <= 64 * 1024 * 1024, 'Pinned assembly byte bound exceeded'
                digest = hashlib.sha256()
                length = 0
                with archive.open(entry) as stream:
                    while chunk := stream.read(1024 * 1024):
                        length += len(chunk)
                        assert length <= entry.file_size
                        digest.update(chunk)
                assert length == entry.file_size
                expected.setdefault(name, []).append({'package': package['path'], 'entry': entry.filename,
                    'bytes': length, 'sha256': digest.hexdigest()})
    return expected


def verify_boogie_assemblies(directory):
    expected = pinned_boogie_assembly_entries()
    actual = [path for path in sorted(directory.glob('Boogie*.dll')) if path.is_file()]
    assert {'Boogie.Core.dll', 'Boogie.ExecutionEngine.dll', 'Boogie.VCGeneration.dll'} <= {path.name for path in actual}
    rows = []
    for path in actual:
        record = file_record(path)
        matches = [entry for entry in expected.get(path.name, []) if
            entry['bytes'] == record['bytes'] and entry['sha256'] == record['sha256']]
        assert matches, 'Test runtime uses an assembly outside the byte-pinned fork packages: ' + path.name
        rows.append({'assembly': path.name, **record, 'matchingPinnedPackageEntries': matches})
    return rows


def capture_core_dependency_inputs():
    # SDK library builds need not copy NuGet DLLs to the library output. Bind
    # the actual SDK-resolved compile/runtime files without changing that default.
    asset_path = Path('Source/DafnyCore/obj/project.assets.json')
    asset_record = file_record(asset_path)
    assert asset_record['bytes'] <= 16 * 1024 * 1024
    assets = json.loads(asset_path.read_text())
    assert assets['version'] == 3 and len(assets['targets']) == 1
    project = assets['project']
    restore = project['restore']
    expected_project = (ROOT / 'Source/DafnyCore/DafnyCore.csproj').resolve()
    assert Path(restore['projectPath']).resolve() == expected_project
    assert Path(restore['projectUniqueName']).resolve() == expected_project
    assert restore['projectName'] == 'DafnyCore' and restore['projectStyle'] == 'PackageReference'
    assert restore['originalTargetFrameworks'] == ['net8.0'] and set(project['frameworks']) == {'net8.0'}
    assert set(restore['frameworks']) == {'net8.0'}
    target_name, target = next(iter(assets['targets'].items()))
    assert target_name in {'net8.0', '.NETCoreApp,Version=v8.0'}
    assert len(assets['packageFolders']) == 1
    package_root = Path(next(iter(assets['packageFolders'])))
    assert package_root.is_dir() and not package_root.is_symlink()
    assert Path(restore['packagesPath']).resolve() == package_root.resolve()
    pinned_boogie = pinned_boogie_assembly_entries()
    rows = []
    projects = []
    total = 0
    for library, declarations in sorted(target.items()):
        metadata = assets['libraries'][library]
        assert declarations['type'] == metadata['type']
        if metadata['type'] != 'package':
            assert metadata['type'] == 'project'
            project_path = (expected_project.parent / metadata['msbuildProject']).resolve()
            assert project_path in {(ROOT / 'Source/DafnyB3Protocol/DafnyB3Protocol.csproj').resolve(),
                (ROOT / 'Source/DafnyRuntime/DafnyRuntime.csproj').resolve()}, 'Unknown Core project reference'
            projects.append({'library': library, 'projectPath': str(project_path),
                'targetFramework': declarations['framework'], 'source': file_record(project_path)})
            continue
        library_path = Path(metadata['path'])
        assert not library_path.is_absolute() and '..' not in library_path.parts
        for kind in ['compile', 'runtime']:
            for value in sorted(declarations.get(kind, {})):
                if not value.endswith('.dll'):
                    continue
                relative = Path(value)
                assert not relative.is_absolute() and '..' not in relative.parts
                path = package_root / library_path / relative
                assert all(not ancestor.is_symlink() for ancestor in path.parents)
                record = file_record(path)
                total += record['bytes']
                assert total <= MAXIMUM_INVENTORY_BYTES and len(rows) < MAXIMUM_INVENTORY_FILES
                row = {'library': library, 'assetKind': kind, 'assetPath': value,
                       'actualPath': str(path), **record}
                if library.startswith('Boogie'):
                    assert library.rsplit('/', 1)[1] == '3.5.5-review.37e4435d', 'Unexpected Boogie package version'
                    matches = [entry for entry in pinned_boogie.get(path.name, []) if
                        entry['bytes'] == record['bytes'] and entry['sha256'] == record['sha256']]
                    assert matches, 'SDK-selected Boogie input is outside the pinned fork package bytes'
                    row['matchingPinnedPackageEntries'] = matches
                rows.append(row)
    assert {row['library'].split('/')[0] for row in projects} == {'DafnyB3Protocol', 'DafnyRuntime'} and len(projects) == 2
    assert {'Boogie.Core', 'Boogie.ExecutionEngine', 'Boogie.VCGeneration'} <= {
        row['library'].split('/')[0] for row in rows if row['assetKind'] == 'compile'}
    return {'assetsFile': {'path': str(asset_path), **asset_record}, 'target': target_name,
            'packageRoot': str(package_root), 'projectReferences': projects, 'files': rows, 'sealSha256': seal(rows)}


def generated_records(root, required):
    rows = directory_records(root)
    by_path = {row['path']: row for row in rows}
    assert set(required) <= by_path.keys(), 'Current build lacks required assembly/config/prelude'
    return rows


def validate_trx():
    path = output / 'focused' / 'result.trx'
    record = file_record(path)
    assert record['bytes'] <= 16 * 1024 * 1024, 'TRX byte bound exceeded'
    root = ET.parse(path).getroot()
    assert root.tag == TRX_NS + 'TestRun'
    rows = root.findall('.//' + TRX_NS + 'UnitTestResult')
    definitions = root.findall('.//' + TRX_NS + 'UnitTest')
    counters = root.findall('.//' + TRX_NS + 'Counters')
    assert len(rows) == len(definitions) == 35 and len(counters) == 1, 'Incomplete exact 35-control denominator'
    counts = {key: int(value) for key, value in counters[0].attrib.items()}
    assert {key: counts[key] for key in ['total', 'executed', 'passed']} == {'total': 35, 'executed': 35, 'passed': 35}
    expected_counter_keys = {'total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted', 'inconclusive',
                            'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected', 'warning',
                            'completed', 'inProgress', 'pending'}
    assert set(counts) == expected_counter_keys and all(value == 0 for key, value in counts.items()
        if key not in {'total', 'executed', 'passed'}), 'Nonpassing or unknown TRX counter'
    ids = {definition.get('id'): definition for definition in definitions}
    assert len(ids) == 35 and all(ids), 'Duplicate/missing test definition identity'
    expected = {(row['className'], row['method']): row['count'] for row in inputs['controls']}
    actual = {key: 0 for key in expected}
    names = set()
    executions = set()
    tests = set()
    receipt = []
    for row in rows:
        test_id, execution, name = row.get('testId'), row.get('executionId'), row.get('testName')
        assert test_id and execution and name and row.get('outcome') == 'Passed'
        assert test_id not in tests and execution not in executions and name not in names, 'Duplicate focused result identity'
        tests.add(test_id); executions.add(execution); names.add(name)
        definition = ids[test_id]
        method = definition.find(TRX_NS + 'TestMethod')
        declared_execution = definition.find(TRX_NS + 'Execution')
        assert method is not None and declared_execution is not None and declared_execution.get('id') == execution
        key = method.get('className'), method.get('name')
        assert key in expected and definition.get('name') == name
        assert name == '.'.join(key) or name.startswith('.'.join(key) + '('), 'TRX name/class/method differs'
        assert method.get('adapterTypeName') == 'executor://xunit/VsTestRunner2/netcoreapp', 'Unexpected test adapter'
        assert Path(method.get('codeBase', '')).resolve() == (test_output / 'DafnyB3Normalizer.Test.dll').resolve(), 'Foreign test assembly'
        actual[key] += 1
        receipt.append({'testId': test_id, 'executionId': execution, 'name': name,
                        'className': key[0], 'method': key[1], 'outcome': row.get('outcome')})
    assert actual == expected and tests == set(ids), 'Named source control denominator differs'
    assert names == {row['displayName'] for row in inputs['expectedExecutionControls']}, 'Exact pinned xUnit control display-name inventory differs'
    return {'expected': 35, 'actual': len(rows), 'allPassed': True, 'trx': record, 'counters': counts,
            'methods': [{'className': key[0], 'method': key[1], 'count': count} for key, count in sorted(actual.items())],
            'tests': receipt}


def validate(name):
    global core_rows, test_rows, test_receipt, package_receipt, java_runtime_jar, core_dependencies
    if name == 'source-head':
        gathered = json.loads((output / 'source-head.txt').read_text())
        assert gathered['head'] == head and re.fullmatch(r'8\.\d+\.\d+', gathered['sdkVersion'])
        sdk_paths = {item['path'] for item in toolchain_rows['directories']}
        dotnet_root = Path(toolchain_rows['executable']['resolvedPath']).parent
        assert str(dotnet_root / 'sdk' / gathered['sdkVersion']) in sdk_paths, 'Selected SDK image is not pinned'
        source_receipt['actualSdkSelection'] = gathered
    elif name == 'packages':
        package_receipt = package_records()
    elif name == 'core-build':
        core_rows = generated_records(core_output, ['DafnyCore.dll', 'DafnyCore.deps.json',
            'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyPrelude.bpl'])
        core_dependencies = capture_core_dependency_inputs()
        assert next(row for row in core_rows if row['path'] == 'DafnyPrelude.bpl')['sha256'] == \
            file_record(Path('Source/DafnyCore/DafnyPrelude.bpl'))['sha256']
        java_runtime_jar = file_record(Path('Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime-4.11.0.jar'))
        (output / 'core-files.json').write_text(json.dumps({'files': core_rows,
            'resolvedDependencyInputs': core_dependencies, 'javaRuntimeDependency': java_runtime_jar}, indent=2) + '\n')
    elif name == 'normalizer-build':
        test_rows = generated_records(test_output, ['DafnyB3Normalizer.Test.dll', 'DafnyB3Normalizer.Test.deps.json',
            'DafnyB3Normalizer.Test.runtimeconfig.json', 'DafnyCore.dll', 'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyPrelude.bpl'])
        source_receipt['testBoogieAssemblies'] = verify_boogie_assemblies(test_output)
        test_flat = {row['path']: row for row in test_rows}
        for row in core_dependencies['files']:
            if row['assetKind'] == 'runtime' and row['library'].startswith('Boogie'):
                copied = test_flat[Path(row['assetPath']).name]
                assert copied['bytes'] == row['bytes'] and copied['sha256'] == row['sha256'], 'Test Boogie runtime differs from Core dependency inputs'
        core_map = {row['path']: row for row in core_rows}
        test_map = {row['path']: row for row in test_rows}
        for path in ['DafnyCore.dll', 'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyPrelude.bpl']:
            assert test_map[path] == core_map[path], 'Tests do not use the freshly built current Core/dependencies'
        deps = json.loads((test_output / 'DafnyB3Normalizer.Test.deps.json').read_text())
        core_libraries = [entry for name, entry in deps['libraries'].items() if name.startswith('DafnyCore/')]
        assert len(core_libraries) == 1 and core_libraries[0]['type'] == 'project', 'Tests use an alternate Core reference'
        runtime = json.loads((test_output / 'DafnyB3Normalizer.Test.runtimeconfig.json').read_text())
        assert runtime['runtimeOptions']['tfm'] == 'net8.0'
        (output / 'test-files.json').write_text(json.dumps(test_rows, indent=2) + '\n')
    elif name == 'guard-focus':
        test_receipt = validate_trx()
    else:
        raise AssertionError('Unknown selected stage')


MAXIMUM_STAGE_LOG_BYTES=32 * 1024 * 1024
STAGE_TIMEOUT_SECONDS=1800
NATURAL_CHILD_GRACE_SECONDS=5
MAXIMUM_CHILD_DIAGNOSTICS=64


def direct_children():
    children = set()
    for task in Path('/proc/self/task').iterdir():
        children.update(int(pid) for pid in (task / 'children').read_text().split())
    return children


def cleanup_children():
    # This dedicated receipt process owns only its stage invocations. Subreaper
    # adoption retains orphaned children even when their Unix sessions differ.
    signalled = 0
    for sig in [signal.SIGTERM, signal.SIGKILL]:
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            while True:
                try:
                    if os.waitpid(-1, os.WNOHANG)[0] == 0:
                        break
                except ChildProcessError:
                    break
            children = direct_children()
            if not children:
                return signalled
            for pid in children:
                try:
                    fd = os.pidfd_open(pid)
                    try:
                        # Recheck adoption after opening the handle. The signal targets
                        # this process instance; a reused numeric PID is never killed.
                        if pid in direct_children():
                            signal.pidfd_send_signal(fd, sig)
                            signalled += 1
                    finally:
                        os.close(fd)
                except ProcessLookupError:
                    pass
            time.sleep(0.01)
    raise RuntimeError('Owned CLI descendants remained after bounded cleanup')


def reap_exited_children():
 while True:
  try:
   if os.waitpid(-1,os.WNOHANG)[0]==0:
    return
  except ChildProcessError:
   return


def reap_completed_adopted_children(stage_pid, receipt):
 # Leave the Popen root exclusively owned by Popen so its real exit status is
 # preserved. Orphaned exited grandchildren must be reaped while a live test
 # host is checking that terminated workers are gone, not only after that host.
 for pid in sorted(direct_children()):
  if pid==stage_pid:
   continue
  try:
   reaped,status=os.waitpid(pid,os.WNOHANG)
  except (ChildProcessError,ProcessLookupError,InterruptedError):
   continue
  if reaped:
   receipt['count']+=1
   assert receipt['count']<=4096, 'Adopted-child reap count exceeds stage bound'
   if len(receipt['records'])<MAXIMUM_CHILD_DIAGNOSTICS:
    receipt['records'].append({'pid':reaped,'waitStatus':status})
   else:
    receipt['recordsTruncated']=True


def child_diagnostics(stage_pid):
 children=sorted(direct_children())
 records=[]
 for pid in children[:MAXIMUM_CHILD_DIAGNOSTICS]:
  record={'pid':pid,'kind':'stage-root' if pid==stage_pid else 'adopted-child'}
  try:
   with (Path('/proc')/str(pid)/'stat').open('rb') as stream:
    stat=stream.read(4096)
   suffix=stat[stat.rindex(b')')+2:].split()
   record['startTimeTicks']=int(suffix[19])
   with (Path('/proc')/str(pid)/'comm').open('rb') as stream:
    record['comm']=stream.read(128).decode('utf-8',errors='replace').rstrip('\n')
   with (Path('/proc')/str(pid)/'cmdline').open('rb') as stream:
    command=stream.read(4097)
   record['cmdline']=command[:4096].replace(b'\0',b' ').decode('utf-8',errors='replace')
   record['cmdlineTruncated']=len(command)>4096
   record['sessionId']=os.getsid(pid)
   record['stillOwned']=pid in direct_children()
  except Exception as error:
   record['inspectionError']=(type(error).__name__+': '+str(error))[:256]
  records.append(record)
 return {'childCount':len(children),'records':records,'recordsTruncated':len(children)>MAXIMUM_CHILD_DIAGNOSTICS}


def run_stage(name, command):
 global cleanup_poisoned
 log_path=output/(name+'.txt')
 actual_code=None
 failure=None
 process=None
 residual=residual_before_grace=None
 cleanup_count=remaining=0
 before_cleanup={'childCount':0,'records':[],'recordsTruncated':False}
 adopted_reaps={'count':0,'records':[],'recordsTruncated':False}
 try:
  assert not cleanup_poisoned and not direct_children(), 'Require empty owned child scope before each stage'
  with log_path.open('wb') as log:
   process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,start_new_session=True,
     cwd="ThirdParty/B3" if name in {"worker-runtime","worker-java"} else None,env=stage_environment)
   deadline=time.monotonic()+STAGE_TIMEOUT_SECONDS
   while process.poll() is None:
    reap_completed_adopted_children(process.pid,adopted_reaps)
    if time.monotonic()>=deadline or log_path.stat().st_size>MAXIMUM_STAGE_LOG_BYTES:
     raise TimeoutError('Stage safety deadline or output byte bound exceeded')
    time.sleep(0.01)
   actual_code=process.wait()
   if time.monotonic()>=deadline:
    raise TimeoutError('Stage safety deadline exceeded')
   residual_before_grace=len(direct_children())
   if residual_before_grace:
    grace_deadline=min(deadline,time.monotonic()+NATURAL_CHILD_GRACE_SECONDS)
    while time.monotonic()<grace_deadline:
     if log_path.stat().st_size>MAXIMUM_STAGE_LOG_BYTES:
      raise TimeoutError('Stage output byte bound exceeded during natural-exit grace')
     reap_exited_children()
     if not direct_children():
      break
     time.sleep(0.01)
    reap_exited_children()
   if time.monotonic()>=deadline:
    raise TimeoutError('Stage safety deadline exceeded during natural-exit grace')
   residual=len(direct_children())
   if residual:
    failure='Completed stage left adopted children after bounded natural-exit grace'
 except Exception as error:
  failure=type(error).__name__+': '+str(error)
 finally:
  try:
   before_cleanup=child_diagnostics(process.pid if process is not None else None)
   if before_cleanup['childCount'] and failure is None:
    failure='Stage left adopted children before bounded cleanup'
  except Exception as error:
   before_cleanup={'inspectionError':(type(error).__name__+': '+str(error))[:256]}
  try:
   cleanup_count=cleanup_children()
   if process is not None:
    process.wait(timeout=5)
   remaining=len(direct_children())
   assert remaining==0, 'Owned stage children remained after bounded cleanup'
  except Exception as error:
   cleanup_poisoned=True
   cleanup_count=None # The bounded helper did not return its final signal count.
   failure=(failure+'; ' if failure else '')+'Cleanup failed: '+str(error)
   try:
    remaining=len(direct_children())
   except Exception:
    remaining=None
 if cleanup_count and failure is None:
  failure='Stage required forced adopted-child cleanup'
 log_bytes=log_path.stat().st_size if log_path.exists() else 0
 truncated=log_bytes>MAXIMUM_STAGE_LOG_BYTES
 if truncated:
  failure=(failure+'; ' if failure else '')+'Output byte bound exceeded'
  # Keep a bounded failed-stage log prefix after the cleanup attempt.
  with log_path.open('r+b') as log:
   log.truncate(MAXIMUM_STAGE_LOG_BYTES)
 log_prefix=b''
 if log_path.exists():
  with log_path.open('rb') as log:
   log_prefix=log.read(MAXIMUM_STAGE_LOG_BYTES)
 code=actual_code if failure is None else 124 if failure.startswith('TimeoutError:') else 2
 if code==0:
  try:
   validate(name)
  except Exception as error:
   failure=type(error).__name__+': '+str(error)
   code=1
 return {'stage':name,'exitCode':code,'actualProcessExitCode':actual_code,'command':command,
   'failure':failure,'naturalChildReapsDuringStage':adopted_reaps,'residualChildrenBeforeGrace':residual_before_grace,
   'residualChildrenAtCompletion':residual,'cleanupSignals':cleanup_count,
   'naturalChildGraceSeconds':NATURAL_CHILD_GRACE_SECONDS,'childrenBeforeCleanup':before_cleanup,
   'remainingAdoptedChildren':remaining,'cleanupPoisoned':cleanup_poisoned,
   'maximumLogBytes':MAXIMUM_STAGE_LOG_BYTES,'observedLogBytes':log_bytes,'logTruncated':truncated,
   'safetyDeadlineSeconds':STAGE_TIMEOUT_SECONDS,
   'hashedLogBytes':len(log_prefix),'logSha256':hashlib.sha256(log_prefix).hexdigest() if log_path.exists() else None}



def run_with_boundaries(name, command):
    before = after = None
    result = None
    try:
        before = boundary(name, 'before')
        result = run_stage(name, command)
    except Exception as error:
        result = {'stage': name, 'exitCode': 1, 'command': command, 'failure': type(error).__name__ + ': ' + str(error)}
    finally:
        try:
            after = boundary(name, 'after')
        except Exception as error:
            failure = type(error).__name__ + ': ' + str(error)
            if result is None:
                result = {'stage': name, 'exitCode': 1, 'command': command, 'failure': failure}
            else:
                result['boundaryFailure'] = failure
                result['exitCode'] = 1
    result['beforeBoundary'] = before
    result['afterBoundary'] = after
    return result


def bounded_tool_stdout(command):
    # Every child belongs to the enclosing source-head owned stage. A failed
    # capture leaves cleanup to that stage's reviewed adoption/pidfd helper.
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    chunks = []
    length = 0
    deadline = time.monotonic() + 60
    with selectors.DefaultSelector() as selected:
        selected.register(process.stdout, selectors.EVENT_READ)
        while selected.get_map():
            assert time.monotonic() < deadline, 'Metadata/SDK selection tool deadline exceeded'
            for key, _ in selected.select(timeout=0.01):
                chunk = os.read(key.fd, 8192)
                if not chunk:
                    selected.unregister(key.fileobj)
                    break
                length += len(chunk)
                assert length <= 1024 * 1024, 'Metadata/SDK selection output byte bound exceeded'
                chunks.append(chunk)
    assert process.wait(timeout=max(0.01, deadline - time.monotonic())) == 0, 'Metadata/SDK selection tool failed'
    return b''.join(chunks).decode('utf-8').strip()


def source_head_child():
    values = {}
    for key, command in [('head', ['git', 'rev-parse', 'HEAD']),
                         ('sdkVersion', ['dotnet', '--version']),
                         ('runtimeSelectionInfo', ['dotnet', '--info']),
                         ('installedRuntimes', ['dotnet', '--list-runtimes'])]:
        values[key] = bounded_tool_stdout(command)
    print(json.dumps(values))


def copy_compiled_artifacts():
    candidates = [('current-core', core_output, core_rows), ('current-tests', test_output, test_rows)]
    observed = directory_records(output)
    expected_bytes = (sum(row['bytes'] for row in observed) +
        sum(sum(row['bytes'] for row in rows) for _, _, rows in candidates if rows is not None))
    assert expected_bytes < MAXIMUM_ARTIFACT_BYTES - 4 * 1024 * 1024, 'Complete current compiled artifacts exceed aggregate bound'
    for label, directory, rows in candidates:
        if rows is not None:
            assert directory_records(directory) == rows, 'Current artifact bytes changed before preservation'
            shutil.copytree(directory, output / label)
            assert directory_records(output / label) == rows, 'Preserved current artifacts differ'


def main():
    global inputs, output, head, source_tree, source_rows, toolchain_rows, source_receipt
    global core_output, test_output, core_rows, test_rows, test_receipt, package_receipt, boundary_receipts, java_runtime_jar, core_dependencies
    global stage_environment, cleanup_poisoned
    output = Path('out/b3-if-guard-focus')
    output.mkdir(parents=True, exist_ok=False)
    head = None
    source_tree = source_rows = toolchain_rows = None
    source_receipt = {}
    boundary_receipts = []
    core_output = Path('Binaries/net8.0')
    test_output = Path('Source/DafnyB3Normalizer.Test/bin/Release/net8.0')
    core_rows = test_rows = test_receipt = package_receipt = java_runtime_jar = core_dependencies = None
    cleanup_poisoned = False
    stage_environment = {**os.environ, 'GRADLE_OPTS': '-Dorg.gradle.daemon=false', 'UseSharedCompilation': 'false',
        'DOTNET_PROCESSOR_COUNT': '1', 'DOTNET_GCHeapHardLimit': '40000000',
        'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER': '1', 'MSBUILDDISABLENODEREUSE': '1',
        'DafnyNormalizerReferenceDirectory': ''}
    results = []
    final_error = None
    try:
        assert Path('/proc/self/task').exists() and hasattr(os, 'pidfd_open') and hasattr(signal, 'pidfd_send_signal')
        assert not direct_children() and ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) == 0
        inputs = json.loads(INPUTS_PATH.read_text())
        validate_selection(inputs)
        assert not core_output.exists() and not test_output.exists(), 'Require fresh current-source output directories'
        validate_source_initial()
        build_flags = ['-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '-p:SourceRevisionId=' + head,
                       '-p:DafnyNormalizerReferenceDirectory=', '--nologo']
        commands = [
            ('source-head', [sys.executable, str(Path(__file__).resolve()), '--source-head']),
            ('packages', ['sh', 'Scripts/fetch-boogie-packages.sh']),
            ('core-build', ['dotnet', 'build', 'Source/DafnyCore/DafnyCore.csproj', *build_flags]),
            ('normalizer-build', ['dotnet', 'build', 'Source/DafnyB3Normalizer.Test/DafnyB3Normalizer.Test.csproj', *build_flags]),
            ('guard-focus', ['dotnet', 'test', 'Source/DafnyB3Normalizer.Test/DafnyB3Normalizer.Test.csproj',
                '-c', 'Release', '--no-build', '--no-restore', '-p:DafnyNormalizerReferenceDirectory=',
                '--filter', FILTER, '--results-directory', str(output / 'focused'),
                '--logger', 'trx;LogFileName=result.trx', '--nologo'])]
        for name, command in commands:
            result = run_with_boundaries(name, command)
            results.append(result)
            print(name, result['exitCode'], result.get('failure') or result.get('boundaryFailure') or '', flush=True)
            if result['exitCode'] != 0 or cleanup_poisoned:
                break
        copy_compiled_artifacts()
    except Exception as error:
        final_error = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            assert not direct_children(), 'Final owned scope is not empty'
        except Exception as error:
            cleanup_poisoned = True
            final_error = (final_error + '; ' if final_error else '') + str(error)
            try:
                signals = cleanup_children()
                final_error += '; final bounded cleanup signals=' + str(signals)
            except Exception as cleanup_error:
                final_error += '; final cleanup failed: ' + str(cleanup_error)
        passed = len(results) == 5 and all(result['exitCode'] == 0 for result in results) and final_error is None and not cleanup_poisoned
        receipt = {'passed': passed, 'scope': 'unsigned-if-guard', 'head': head,
            'focusedControlCountExpected': 35, 'fullNormalizerCountSourceDefined': 469, 'corpusCountSourceDefined': 79,
            'completeLibraryVerified': False, 'libraryBinaryProduced': False, 'workerRuntimeVerified': False,
            'corpusVerified': False, 'defaultCompatibilityVerified': False, 'backendAccepted': False,
            'source': source_receipt, 'stages': results, 'stageBoundaries': boundary_receipts,
            'packages': package_receipt, 'resolvedCoreDependencies': core_dependencies, 'javaRuntimeDependency': java_runtime_jar,
            'focusedControls': test_receipt, 'finalError': final_error,
            'cleanupPoisoned': cleanup_poisoned, 'maximumArtifactBytes': MAXIMUM_ARTIFACT_BYTES,
            'provenanceScope': 'Filesystem byte pins at stage boundaries; not atomic execution-image attestation',
            'ownershipScope': 'Dedicated Linux child-subreaper/pidfd scope; no cgroup or arbitrary escape claim'}
        summary = output / 'summary.json'
        summary.write_text(json.dumps(receipt, indent=2) + '\n')
        try:
            rows = directory_records(output)
            assert sum(row['bytes'] for row in rows) <= MAXIMUM_ARTIFACT_BYTES, 'Aggregate output byte ceiling exceeded'
            (output / 'artifact-inventory.json').write_text(json.dumps({'files': rows, 'sealSha256': seal(rows)}, indent=2) + '\n')
            assert sum(row['bytes'] for row in directory_records(output)) <= MAXIMUM_ARTIFACT_BYTES, 'Final artifact inventory exceeds aggregate bound'
        except Exception as error:
            receipt['passed'] = False
            receipt['artifactFailure'] = type(error).__name__ + ': ' + str(error)
            summary.write_text(json.dumps(receipt, indent=2) + '\n')
        wording = 'PASS: current Core/test compile and 35 selected controls only' if receipt['passed'] else 'NOT GREEN: selected source/compile/control receipt incomplete or failed'
        print(wording, flush=True)
        if path := os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(path, 'a') as stream:
                stream.write('## Unsigned If guard focused receipt\n\n' + wording + '\n\n'
                    'Library, worker, corpus and default compatibility acceptance remain false.\n')
    return 0


if __name__ == '__main__':
    if sys.argv[1:] == ['--source-head']:
        source_head_child()
    else:
        assert not sys.argv[1:], 'Unexpected coordinator argument'
        raise SystemExit(main())
