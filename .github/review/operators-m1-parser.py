#!/usr/bin/env python3
"""Compare generated scanner bytes, token IDs, lookahead sets and syntax diagnostics."""
import hashlib
import json
from pathlib import Path
import re
import sys


def fingerprint(root):
    folder = Path(root) / 'Source/DafnyCore'
    parser = (folder / 'Parser.cs').read_text()
    tokens = re.findall(r'^\s*(?:public )?const int .*;', parser, re.M)
    sets = re.search(r'static readonly bool\[,] set = \{.*?\n\s*\};', parser, re.S)
    errors = re.search(r'string GetSyntaxErrorString\(int n\) \{.*?\n\s*return s;', parser, re.S)
    assert tokens and sets and errors, 'generated parser sections must be present'
    return {'scanner_sha256': hashlib.sha256((folder / 'Scanner.cs').read_bytes()).hexdigest(),
            'tokens': tokens, 'lookahead_sets': sets[0], 'syntax_errors': errors[0]}


baseline, candidate, output = sys.argv[1:]
result = {'baseline': fingerprint(baseline), 'candidate': fingerprint(candidate)}
result['passed'] = result['baseline'] == result['candidate']
Path(output).write_text(json.dumps(result, indent=2))
if not result['passed']:
    raise SystemExit('generated lexer/parser tables or syntax diagnostics changed')
print('generated scanner, token IDs, lookahead sets and syntax diagnostics are identical')
