#!/usr/bin/env python3
"""Reverify replacements, then compare exact client Boogie and SMT task bytes."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


SOURCE = """module Library {
  export reveals T provides T.Step, H, Closed, Fixed, Weight, Small
  trait {:termination false} T { method Step() decreases {:global} 0 }
  method H(t: T) decreases {:global} 1 { BODY }
  method Private(t: T) decreases {:global} 1 { PRIVATE }
  EXTRA
  method {:termination_closed} Closed() { CLOSED }
  function {:termination_closed} Fixed(): nat { VALUE }
  const {:termination_closed} Weight: nat := VALUE
  type {:termination_closed} Small = x: int | CONSTRAINT witness 1
}
module Client {
  import Library
  method Call(t: Library.T) decreases {:global} 1 {
    Library.H(t); Library.Closed(); var n := Library.Fixed(); var w := Library.Weight; CONTROL
  }
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('binary', type=lambda s: Path(s).resolve())
    parser.add_argument('solver', type=lambda s: Path(s).resolve())
    parser.add_argument('output', type=lambda s: Path(s).resolve())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    source = args.output / 'substitution.dfy'
    boogie = args.output / 'task.bpl'
    smt = args.output / 'task.smt2'
    evidence = []
    for refresh in ['false', 'true']:
        for consistent in ['false', 'true']:
            baseline = None
            for index, body in enumerate(['', 't.Step();', 'Private(t);']):
                text = SOURCE.replace('BODY', body).replace('PRIVATE', 't.Step();' if index else '')
                text = text.replace('EXTRA', 'method Hidden(t: T) decreases {:global} 1 { t.Step(); }' if index else '')
                text = text.replace('CLOSED', 'var n := 1;' if index else '')
                text = text.replace('VALUE', str(bool(index) * 1))
                text = text.replace('CONSTRAINT', ('1' if index else '0') + ' <= x')
                source.write_text(text.replace('CONTROL', ''))
                common = [str(args.binary), 'verify', str(source), '--modular-termination',
                          '--solver-path', str(args.solver), '--cores', '1', '--resource-limit', '16000000',
                          '--verification-time-limit', '0', '--standard-libraries:false', '--show-snippets:false',
                          '--type-system-refresh:' + refresh, '--general-traits:legacy',
                          '--consistent-obligation-checks:' + consistent,
                          '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']
                name = f'{refresh}-{consistent}-{index}'

                def run(label, extra, expected):
                    result = subprocess.run(common + extra, capture_output=True, timeout=90)
                    (args.output / (name + '-' + label + '.log')).write_bytes(result.stdout + result.stderr)
                    if result.returncode != expected:
                        raise AssertionError(f'{name} {label}: expected {expected}, got {result.returncode}')
                    return result

                run('producer-and-client', [], 0)
                smt.unlink(missing_ok=True)
                run('client', ['--filter-symbol', 'Client', '--bprint', str(boogie), '--solver-log', str(smt)], 0)
                client_boogie = boogie.with_name('task_Client.bpl').read_bytes()
                client_smt = smt.read_bytes()
                if baseline is None:
                    baseline = (client_boogie, client_smt)
                if baseline != (client_boogie, client_smt):
                    raise AssertionError(name + ': client semantic input changed')
                evidence.append({'case': name, 'boogie': hashlib.sha256(client_boogie).hexdigest(),
                                 'smt': hashlib.sha256(client_smt).hexdigest()})
                source.write_text(text.replace('CONTROL', 'assert false;'))
                control = run('false-control', ['--filter-symbol', 'Client'], 4)
                if not any(message in control.stdout for message in [b'this assertion could not be proved', b'assertion might not hold']):
                    raise AssertionError(name + ': missing reachable assertion failure')
            print(f'Identical client Boogie/SMT and reachable controls: refresh={refresh}, consistent={consistent}', flush=True)
    (args.output / 'evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')


if __name__ == '__main__':
    main()
