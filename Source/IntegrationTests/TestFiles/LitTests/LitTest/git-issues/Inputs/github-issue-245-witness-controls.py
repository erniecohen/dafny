"""Unchecked witnesses and ordinary helper parameters retain their effects (issue 245)."""
import subprocess
import sys
from pathlib import Path

executable = Path(sys.argv[1]).resolve()
output = Path(sys.argv[2]).resolve()
output.mkdir(parents=True, exist_ok=True)
source = Path(__file__).with_name('github-issue-245-witness-unchecked.dfy').read_text()
common = ['--modular-termination', '--decreases-orders', '--type-system-refresh',
          '--general-traits=datatype', '--standard-libraries:false',
          '--warn-unchecked-termination=false', '--show-snippets:false', '--use-basename-for-filename']
variants = {
    'verify-false': source,
    'termination-false': source.replace('{:verify false}', '{:termination false}'),
    'axiom': source.replace('{:verify false}', '{:axiom}'),
    'extern': source.replace('{:verify false}', '{:extern}'),
    'assumed': source.replace('{:verify false}', '').replace(
        'datatype Bad', 'datatype {:AssumeCrossModuleTermination} Bad'),
    'inherited-assumption': source.replace('{:verify false}', '').replace(
        '  datatype Bad extends Wfo<nat>',
        '  trait {:AssumeCrossModuleTermination} Assumed extends Wfo<nat> {}\n  datatype Bad extends Assumed'),
    'helper': source.replace('{:verify false}', '').replace(
        'var x := c(0)', 'var x := Helper(c)').replace(
        '  datatype Bad', '  ghost function Helper(c: Chain<nat>): nat { c(0) }\n  datatype Bad'),
}
for name, text in variants.items():
    path = output / (name + '.dfy')
    path.write_text(text)
    result = subprocess.run(['dotnet', str(executable), 'resolve', *common, str(path)],
                            text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (output / (name + '.log')).write_text(result.stdout)
    if result.returncode != 2 or "bound of 'Unchecked.Bound' can reach dispatch" not in result.stdout:
        raise AssertionError(f'{name}: expected the unknown-code bound rejection, got {result.returncode}\n{result.stdout}')
    print(f'{name}: rejected as unknown code')
