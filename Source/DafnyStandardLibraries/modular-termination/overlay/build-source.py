"""Materialize the reviewed checked-Std overlay without modifying the legacy source tree.

Usage: python3 build-source.py OUTPUT_DIRECTORY
The destination must not exist. The exact overlaid source is subsequently verified and
retained by the normal library producer. Its recipe digest includes this file and every
overlay input, so it is part of the retained source, not unpublished loader inference.
"""
from pathlib import Path
import hashlib
import os
import shutil
import subprocess
import sys

here = Path(__file__).resolve().parent
destination = Path(sys.argv[1]).resolve()
shutil.copytree(here.parents[1] / 'src/Std', destination)
patch = here / 'Actions.patch'
# The output may be under a checkout. Prevent Git from interpreting these paths
# relative to that checkout rather than to the freshly copied source directory.
environment = dict(os.environ, GIT_CEILING_DIRECTORIES=str(destination.parent))
subprocess.run(['git', 'apply', '--check', str(patch)], cwd=destination, env=environment, check=True)
subprocess.run(['git', 'apply', str(patch)], cwd=destination, env=environment, check=True)
shutil.copyfile(here / 'CallPermissions.dfy', destination / 'CallPermissions.dfy')
project = destination / 'dfyconfig.toml'
configuration = project.read_text()
legacy_warning_policy = """# Std.Actions has 20 classes and traits with @AssumeCrossModuleTermination, a known
# unchecked premise tracked for migration in https://github.com/erniecohen/dafny/issues/211.
# Programs that use the library are not warned about them in any case.
warn-unchecked-termination = false
"""
if configuration.count(legacy_warning_policy) != 1:
    raise SystemExit('the legacy Std warning policy changed; review the checked recipe')
configuration = configuration.replace('resource-limit = 1000000', 'resource-limit = 16000000')
project.write_text(configuration.replace(legacy_warning_policy,
    '# This source has checked termination contracts and records its named ghost premises.\n'
    'warn-unchecked-termination = true\n'
    'modular-termination-trust = "standard-library"\n'))
target_project = destination / 'TargetSpecific/dfyconfig.toml'
target_configuration = target_project.read_text()
# The normal producer passes its freshly certified common library explicitly. A generated
# source tree must never retain the legacy unrecorded binary from the upstream project.
library_start = target_configuration.index('library = [')
library_end = target_configuration.index(']', library_start) + 1
target_configuration = (target_configuration[:library_start] + 'library = []' +
                        target_configuration[library_end:])
target_configuration = target_configuration.replace('resource-limit = 1000000', 'resource-limit = 16000000')
target_configuration += ('\n'
                         'modular-termination-trust = "standard-library"\n'
                         'modular-termination-runtime-trust = "standard-library"\n')
target_project.write_text(target_configuration)
inputs = ['build-source.py', 'Actions.patch', 'CallPermissions.dfy']
digest = hashlib.sha256(b''.join(name.encode() + b'\0' + (here / name).read_bytes()
                               for name in inputs)).hexdigest()
(destination / 'CheckedOverlay.dfy').write_text(
    'module Std.CheckedOverlay {\n'
    '  const {:termination_closed} Recipe: string := "' + digest + '"\n}\n')
