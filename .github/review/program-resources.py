#!/usr/bin/env python3
"""Record per-program RU and batch headroom without changing verdicts or limits."""
import argparse
from collections import defaultdict
from decimal import Decimal, InvalidOperation
import hashlib
import json
import os
from pathlib import Path
import platform
import re

LIMIT_ATTRIBUTE = re.compile(r'\{:\s*(resource_limit|rlimit|timeLimitMultiplier)\s+("[^"\n]*"|[\d.eE+]+)\s*\}')
AT_ATTRIBUTE = re.compile(r'@(ResourceLimit|TimeLimitMultiplier)\s*\(([^)]*)\)')
MEMBER = re.compile(r'((?:@\w+(?:\([^)]*\))?\s*)*)(?:(?:ghost|static|opaque|twostate|least|greatest)\s+)*'
                    r'\b(?:method|lemma|function|predicate)\s*((?:\{:[^}]*\}\s*)*)([\w\'?#]+)')


def scope_ranges(text):
    # Named module/class/trait scopes used by the resource-annotated members.
    # Strings are blanked for brace matching, so interpolation text is harmless.
    string_pattern = r'"(?:\\.|[^"\\])*"'
    char_pattern = r"'(?:[^'\\\n]|\\(?:u\{[0-9a-fA-F]+\}|.))'"
    braces = re.sub(string_pattern + '|' + char_pattern, lambda m: ' ' * len(m[0]), text)
    pairs, stack = {}, []
    for index, char in enumerate(braces):
        if char == '{':
            stack.append(index)
        elif char == '}' and stack:
            pairs[stack.pop()] = index
    result = []
    for match in re.finditer(r'\b(module|class|trait|datatype|codatatype)\s+(?:\{:[^}]*\}\s*)*([\w.]+)', text):
        start = braces.find('{', match.end())
        # Module aliases have no body of their own.
        if start in pairs and not (match[1] == 'module' and '=' in braces[match.end():start]):
            result.append((start, pairs[start], match[2]))
    return result


def number(value):
    try:
        result = Decimal(str(value).strip('"'))
    except InvalidOperation as error:
        raise ValueError('Invalid resource limit') from error
    if result < 0 or result != result.to_integral_value():
        raise ValueError('Invalid resource limit')
    return int(result)


def option_limit(command):
    result = None
    for index, token in enumerate(command):
        match = re.fullmatch(r'--resource-limit(?:[=:](.*))?', token)
        if match:
            result = number(match[1] if match[1] is not None else command[index + 1])
    if result is None:
        raise ValueError('Missing invocation resource limit')
    return result


def overrides(paths):
    """Read the literal member attributes used by these CI input trees.

    Unrecognized limit syntax fails closed instead of inventing a limit. Names
    with conflicting overrides are rejected when a corresponding batch is read.
    """
    result = defaultdict(set)
    for path in sorted(set(paths)):
        text = path.read_text()
        # Preserve string literals; comments must not create apparent attributes.
        text = re.sub(r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/',
                      lambda m: m[0] if m[0].startswith('"') else ' ' * len(m[0]), text)
        found, found_at = set(), set()
        scopes = scope_ranges(text)
        for member in MEMBER.finditer(text):
            values = {}
            for attr in LIMIT_ATTRIBUTE.finditer(member[2]):
                values[attr[1]] = number(attr[2])
                found.add(member.start(2) + attr.start())
            for attr in AT_ATTRIBUTE.finditer(member[1]):
                key = {'ResourceLimit': 'resource_limit', 'TimeLimitMultiplier': 'timeLimitMultiplier'}[attr[1]]
                values[key] = number(attr[2])
                found_at.add(member.start(1) + attr.start())
            if values:
                prefix = [name for start, end, name in scopes if start < member.start() < end]
                result['.'.join(prefix + [member[3]])].add(tuple(sorted(values.items())))
        expected = {attr.start() for attr in re.finditer(
            r'\{:\s*(?:resource_limit|rlimit|timeLimitMultiplier)\b', text)}
        expected_at = {attr.start() for attr in AT_ATTRIBUTE.finditer(text)}
        if expected != found or expected_at != found_at:
            raise ValueError('Unsupported resource-limit attribute in ' + path.name)
    return result


def limit_for(name, default, attributes):
    name = name.split(' (')[0]
    values = attributes.get(name, set())
    if not values:
        # A containing implicit/default module may be absent from the source.
        candidates = [value for key, value in attributes.items() if name.endswith('.' + key)]
        values = set().union(*candidates) if candidates else set()
    if len(values) > 1:
        raise ValueError('Ambiguous limit for ' + name)
    values = dict(next(iter(values), ()))
    if 'resource_limit' in values:
        return values['resource_limit']
    if 'timeLimitMultiplier' in values and default:
        return min(4294967295, default * values['timeLimitMultiplier'])
    if 'rlimit' in values:
        return min(4294967295, 1000 * values['rlimit'])
    return default


def sources(path):
    """The root program and its relative includes, for member-level overrides."""
    seen, pending = set(), [path]
    while pending:
        current = pending.pop().resolve()
        if current in seen:
            continue
        seen.add(current)
        for name in re.findall(r'^\s*include\s+"([^"]+)"', current.read_text(), re.M):
            pending.append(current.parent / name)
    return seen


def record(program, log, default, attributes):
    if not log.exists() or not log.read_text().strip():
        return {'program': program, 'unavailable': True}, []
    batches = []
    for declaration in json.loads(log.read_text())['verificationResults']:
        limit = limit_for(declaration['name'], default, attributes)
        for vc in declaration['vcResults']:
            ru = int(vc['resourceCount'])
            batches.append({'program': program, 'declaration': declaration['name'], 'vc': vc['vcNum'],
                            'ru': ru, 'limit': limit, 'outcome': vc['outcome'],
                            'near_limit': limit > 0 and 4 * ru >= 3 * limit})
    return {'program': program, 'total_ru': sum(b['ru'] for b in batches),
            'max_batch_ru': max((b['ru'] for b in batches), default=0),
            'batch_count': len(batches), 'unavailable': False}, batches


def collect(kind, measurements, source_root, verdicts):
    programs, batches = [], []
    if kind == 'suite':
        expected = {line.split('\t')[0] for line in verdicts.read_text().splitlines() if line}
        for command in sorted(measurements.rglob('command.json')):
            program = command.parent.relative_to(measurements).as_posix()
            args = json.loads(command.read_text())
            log = command.with_name('results.json')
            has_batches = log.exists() and log.read_text().strip() and any(
                declaration['vcResults'] for declaration in json.loads(log.read_text())['verificationResults'])
            # Invalid invocations/includes are expected diagnostic fixtures. They
            # have no solver limit to interpret and no proof cost to classify.
            default = option_limit(args) if has_batches else 0
            attributes = overrides(sources(source_root / program)) if has_batches else {}
            row, vcs = record(program, log, default, attributes)
            programs.append(row); batches.extend(vcs)
        if {row['program'] for row in programs} != expected:
            raise ValueError('Resource/selected-program coverage differs')
    else:
        attributes = overrides((source_root / 'src').rglob('*.dfy'))
        expected = {line.split('\t')[0][4:] for line in verdicts.read_text().splitlines() if line.startswith('run ')}
        configs = {'Std': source_root / 'src/Std/dfyconfig.toml'}
        configs.update({key: source_root / 'src/Std/TargetSpecific/dfyconfig.toml' for key in expected - {'Std'}})
        for program in sorted(expected):
            match = re.search(r'^resource-limit\s*=\s*(\d+)', configs[program].read_text(), re.M)
            if not match:
                raise ValueError('Missing library resource limit')
            row, vcs = record(program, measurements / (program + '.verification.json'), number(match[1]), attributes)
            if row['unavailable']:
                raise ValueError('Missing library proof log: ' + program)
            programs.append(row); batches.extend(vcs)
        if len(programs) != 7:
            raise ValueError('Missing library parts')
    return programs, batches


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('kind', choices=['suite', 'library'])
    parser.add_argument('measurements', type=Path)
    parser.add_argument('source_root', type=Path)
    parser.add_argument('verdicts', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--solver', type=Path)
    parser.add_argument('--archive', type=Path)
    args = parser.parse_args()
    programs, batches = collect(args.kind, args.measurements, args.source_root, args.verdicts)
    report = {'source': os.environ.get('GITHUB_SHA', ''), 'platform': platform.system(),
              'programs': programs, 'batches': batches,
              'near_limit': [batch for batch in batches if batch['near_limit']],
              'threshold': 'At least 75% of a finite per-batch limit, including overruns; never divide total program RU by a batch limit.'}
    for name in ['solver', 'archive']:
        value = getattr(args, name)
        if value:
            report[name + '_sha256'] = hashlib.sha256(value.read_bytes()).hexdigest()
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'summary.json').write_text(json.dumps(report, indent=2) + '\n')
    (args.output / 'programs.tsv').write_text('program\ttotal_ru\tmax_batch_ru\tbatch_count\tunavailable\n' + ''.join(
        '\t'.join(str(row.get(key, '')) for key in ['program', 'total_ru', 'max_batch_ru', 'batch_count', 'unavailable']) + '\n'
        for row in programs))
    (args.output / 'batches.tsv').write_text('program\tdeclaration\tvc\tru\tlimit\toutcome\tnear_limit\n' + ''.join(
        '\t'.join(str(row[key]) for key in ['program', 'declaration', 'vc', 'ru', 'limit', 'outcome', 'near_limit']) + '\n'
        for row in batches))
    text = ('\n### Per-program resource headroom\n\n' + report['threshold'] + '\n\n' +
            f'{len(programs)} programs/parts; {len(batches)} proof batches; {len(report["near_limit"])} near/over-limit batches. '
            'Full program totals and batch limits/counts are retained in the verdict artifact.\n\n' +
            '| Program | Declaration / VC | RU | Limit |\n|---|---|---:|---:|\n' + ''.join(
                f'| `{b["program"]}` | `{b["declaration"]} / {b["vc"]}` | {b["ru"]} | {b["limit"]} |\n'
                for b in report['near_limit']))
    (args.output / 'summary.md').write_text(text)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as handle:
            handle.write(text)
    print(text)


if __name__ == '__main__':
    main()
