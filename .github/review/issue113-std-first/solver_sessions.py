#!/usr/bin/env python3
"""Pure byte-preserving structure audit of retained SMT-Lib session logs.

Never executes, rewrites, sorts or normalizes commands. Only `check` self-tests
and `files` analyses are available. Raw bytes and complete session histories
remain authoritative; a VC assertion hash alone does not imply query equality.
"""
import argparse
import hashlib
import json
from pathlib import Path

MAX_PARSE_BYTES = 256 * 1024 * 1024
SPACE = b' \t\r\n'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(file):
    digest = hashlib.sha256()
    with file.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def tokens(data):
    """SMT tokens as (kind,start,end) byte ranges, excluding whitespace/comments."""
    i = 0
    while i < len(data):
        if data[i] in SPACE:
            i += 1
        elif data[i] == ord(';'):
            end = data.find(b'\n', i)
            i = len(data) if end < 0 else end + 1
        elif data[i] in b'()':
            yield chr(data[i]), i, i + 1
            i += 1
        elif data[i] == ord('"'):
            start = i
            i += 1
            while i < len(data):
                if data[i] == ord('"'):
                    if i + 1 < len(data) and data[i + 1] == ord('"'):
                        i += 2
                    else:
                        i += 1
                        yield 'string', start, i
                        break
                else:
                    i += 1
            else:
                raise ValueError('Unterminated SMT string at byte ' + str(start))
        elif data[i] == ord('|'):
            start = i
            end = data.find(b'|', i + 1)
            if end < 0:
                raise ValueError('Unterminated quoted SMT symbol at byte ' + str(start))
            if b'\\' in data[i + 1:end]:
                # SMT-Lib quoted symbols cannot contain a backslash. Preserve
                # the raw file and report unsupported syntax rather than guess.
                raise ValueError('Unsupported backslash in quoted symbol at byte ' + str(start))
            i = end + 1
            yield 'symbol', start, i
        else:
            start = i
            while i < len(data) and data[i] not in SPACE + b'();"|':
                i += 1
            yield 'atom', start, i


def commands(data):
    """Return exact complete top-level command ranges and lexical head tokens."""
    result, depth, start, direct = [], 0, None, []
    for kind, first, end in tokens(data):
        if kind == '(':
            if depth == 0:
                start, direct = first, []
            depth += 1
        elif kind == ')':
            depth -= 1
            if depth < 0:
                raise ValueError('Unmatched close parenthesis at byte ' + str(first))
            if depth == 0:
                result.append({'start': start, 'end': end, 'direct_tokens': direct})
        elif depth == 0:
            raise ValueError('Unexpected top-level token at byte ' + str(first))
        elif depth == 1:
            direct.append(data[first:end].decode('utf-8'))
    if depth:
        raise ValueError('Unterminated SMT command at byte ' + str(start))
    return result


def inspect_bytes(data):
    data.decode('utf-8')  # Reject malformed text; do not replace semantic bytes.
    sequence = commands(data)
    markers = []
    for index, command in enumerate(sequence):
        direct = command['direct_tokens']
        if len(direct) == 3 and direct[:2] == ['set-info', ':boogie-vc-id']:
            markers.append((index, direct[2]))
    vcs, prefix, prefix_end = [], hashlib.sha256(), 0
    for m, (index, vc_id) in enumerate(markers):
        following = markers[m + 1][0] if m + 1 < len(markers) else len(sequence)
        first, last = sequence[index]['start'], sequence[following - 1]['end']
        prefix.update(data[prefix_end:first])
        prefix_end = first
        vcs.append({'vc_id_lexical_token': vc_id, 'marker_command_index': index,
            'start_byte': first, 'end_byte': last,
            'preceding_complete_session_bytes_sha256': prefix.hexdigest(),
            'raw_marker_to_next_marker_segment_sha256': sha(data[first:last]),
            'assertions': [{'command_index': n, 'start_byte': sequence[n]['start'], 'end_byte': sequence[n]['end'],
                            'raw_command_sha256': sha(data[sequence[n]['start']:sequence[n]['end']])}
                           for n in range(index + 1, following)
                           if sequence[n]['direct_tokens'][:1] == ['assert']]})
    return {'parsed': True, 'command_count': len(sequence), 'vc_markers': len(markers),
        'vc_segments': vcs,
        'command_manifest': [{'index': n, 'start_byte': command['start'], 'end_byte': command['end'],
                              'head': command['direct_tokens'][0] if command['direct_tokens'] else None,
                              'raw_command_sha256': sha(data[command['start']:command['end']])}
                             for n, command in enumerate(sequence)],
        'mapping_boundary': 'Markers retain exact lexical IDs when emitted. No guessed per-batch mapping if absent; marker segments can include pop/reset/new background commands for the next query. Complete preceding-session hash and raw log preserve that context.'}


def inspect_file(file):
    result = {'file': file.name, 'bytes': file.stat().st_size, 'raw_sha256': file_sha(file)}
    if result['bytes'] > MAX_PARSE_BYTES:
        return {**result, 'parsed': False, 'reason': 'Raw file retained; parser safety cap exceeded'}
    try:
        return {**result, **inspect_bytes(file.read_bytes())}
    except (ValueError, UnicodeError) as error:
        return {**result, 'parsed': False, 'reason': str(error)}


def check():
    sample = ('; header (not a command)\n(declare-fun |f(λ)| () String)\n'
              '(set-info :boogie-vc-id |P#0|)\n(assert (= |f(λ)| "()"";quoted"))\n'
              '(check-sat)\n(pop 1)\n(reset)\n'
              '(set-info :boogie-vc-id "P#1")\n(assert true)\n(check-sat)\n').encode()
    parsed = inspect_bytes(sample)
    assert parsed['command_count'] == 9 and parsed['vc_markers'] == 2
    assert [vc['vc_id_lexical_token'] for vc in parsed['vc_segments']] == ['|P#0|', '"P#1"']
    assert all(len(vc['assertions']) == 1 for vc in parsed['vc_segments'])
    first = parsed['vc_segments'][0]['assertions'][0]
    raw = sample[first['start_byte']:first['end_byte']]
    assert raw == '(assert (= |f(λ)| "()"";quoted"))'.encode()
    assert first['raw_command_sha256'] == sha(raw)
    assert all(vc['preceding_complete_session_bytes_sha256'] == sha(sample[:vc['start_byte']])
               for vc in parsed['vc_segments'])
    assert parsed['command_manifest'][5]['head'] == 'reset'
    assert inspect_bytes(b'(assert true)\n')['vc_markers'] == 0
    for invalid in (b'(assert true', b'(assert "bad)', b'(assert |bad)', b')', b'bare', b'(assert |bad\\id|)'):
        try:
            inspect_bytes(invalid)
        except ValueError:
            pass
        else:
            raise AssertionError('Parser accepted malformed/unsupported SMT')
    print(json.dumps({'pure_SMT_token_checks': True, 'normalization': 'none', 'engines_invoked': False}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('check', 'files'))
    parser.add_argument('paths', nargs='*', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if args.command == 'check':
        check()
    else:
        result = {'boundary': 'Pure retained-log metadata audit; no replay or normalization',
                  'sessions': [inspect_file(file) for file in args.paths]}
        text = json.dumps(result, indent=2) + '\n'
        if args.output:
            args.output.write_text(text)
        else:
            print(text)


if __name__ == '__main__':
    main()
