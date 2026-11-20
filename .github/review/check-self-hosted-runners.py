#!/usr/bin/env python3
"""Fail closed on runner selectors; actionlint separately validates YAML structure.

Only literal complete label arrays and repository-local reusable workflows are
accepted. Expressions, matrix selectors, aliases and multiline runner selectors
must be reviewed rather than guessed at by this check.
"""
from pathlib import Path
import re
import sys

LABELS = '[self-hosted, macOS, ARM64, fork-ci]'


def check(directory):
    count = 0
    errors = []
    for workflow in sorted([*directory.glob('*.yml'), *directory.glob('*.yaml')]):
        text = workflow.read_text()
        if re.search(r'''["']?fetch-depth["']?\s*:\s*["']?0(?:\s|["']|$)''', text):
            errors.append(f'{workflow.name}: unbounded checkout history is forbidden')
        if '-'.join(('mac', 'studio')) in text.lower():
            errors.append(f'{workflow.name}: retired runner label is forbidden')
        jobs = text.split('\njobs:\n', 1)[-1]
        for match in re.finditer(r'^  ([\w-]+):\n(.*?)(?=^  [\w-]+:\n|\Z)', jobs, re.M | re.S):
            name, body = match.groups()
            runner = re.findall(r'^    [\'"]?runs-on[\'"]?:([^\n]*)$', body, re.M)
            reusable = re.findall(r'^    uses: (.*)$', body, re.M)
            if runner:
                count += 1
                if runner != [' ' + LABELS]:
                    errors.append(f'{workflow.name}/{name}: runner must be {LABELS}')
            elif len(reusable) != 1 or not reusable[0].startswith('./.github/workflows/'):
                errors.append(f'{workflow.name}/{name}: missing literal runner or local reusable workflow')
        if '\njobs:\n' not in text:
            errors.append(f'{workflow.name}: unsupported jobs mapping')
    return count, errors


if __name__ == '__main__':
    count, errors = check(Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parents[1] / 'workflows')
    for error in errors:
        print(error, file=sys.stderr)
    print(f'Checked {count} executable jobs; {len(errors)} runner policy errors')
    sys.exit(bool(errors))
