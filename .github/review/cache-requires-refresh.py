"""Cache admission rejects the legacy type system before parsing or cache I/O."""
import os
from pathlib import Path
import subprocess
import sys

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
option = sys.argv[5]
assert option in ("--translation-cache", "--verification-cache", "--cache-dir")
# --cache-dir is an alias of --verification-cache, the cache store's root.
named = "--verification-cache" if option == "--cache-dir" else option
assert not destination.exists(), "use a fresh test directory"
destination.mkdir(parents=True)
invalid = destination / "invalid.dfy"
invalid.write_text("this is not a Dafny program\n")
for name, project_value, cli_value in [
    ("default", None, None),
    ("cli-false", None, False),
    ("project-false", False, None),
    ("cli-overrides-project", True, False),
]:
    input_file = invalid
    if project_value is not None:
        input_file = destination / (name + ".toml")
        input_file.write_text('includes = ["invalid.dfy"]\n[options]\ntype-system-refresh = '
                              + str(project_value).lower() + "\n")
    cache = destination / (name + "-cache")
    profile = destination / (name + ".jsonl")
    args = ["dotnet", str(exe), "verify", str(input_file), option]
    if option in ("--verification-cache", "--cache-dir"):
        args += [str(cache)]
    if cli_value is not None:
        args += ["--type-system-refresh:" + str(cli_value).lower()]
    args += ["--pipeline-profile", str(profile)]
    result = subprocess.run(args, capture_output=True, timeout=60)
    assert result.returncode == 2, (name, result.returncode, result.stdout, result.stderr)
    assert result.stdout.decode().strip() == "Error: " + named + " requires --type-system-refresh", result.stdout
    assert not result.stderr, result.stderr
    assert not cache.exists() and not profile.exists(), "rejected options must not start the pipeline"

# A store named only by the environment is not an error without --type-system-refresh: nothing is cached.
if option == "--cache-dir":
    store = destination / "environment-store"
    environment = dict(os.environ, DAFNY_CACHE_DIR=str(store))
    result = subprocess.run(["dotnet", str(exe), "resolve", str(source), "--type-system-refresh:false"],
                            capture_output=True, timeout=60, env=environment)
    assert result.returncode == 0, (result.returncode, result.stdout, result.stderr)
    assert not store.exists(), "the legacy type system must not use the cache store"

# The owner restriction changes cache admission, not ordinary legacy verification.
result = subprocess.run(["dotnet", str(exe), "verify", str(source), "--type-system-refresh:false",
                         "--filter-symbol", "NoSuchDeclaration", "--solver-path", str(solver)],
                        capture_output=True, timeout=60)
assert result.returncode == 0, (result.returncode, result.stdout, result.stderr)
print("cache legacy-type validation passed")
