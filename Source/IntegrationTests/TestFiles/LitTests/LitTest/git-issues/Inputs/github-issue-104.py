"""Exercise CLI/project output precedence and backend file placement in fresh directories."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

assembly = Path(sys.argv[1]).resolve()
root = Path(sys.argv[2]).resolve()
record_only = "--record" in sys.argv[3:]
if root.exists():
    shutil.rmtree(root)
root.mkdir(parents=True)
release = os.environ.get("DAFNY_RELEASE")
launcher = [str(Path(release) / "dafny")] if release else ["dotnet", str(assembly)]
records = []
failures = []

def case(name, command, mode, project_output="nested/result", extra=(), target="cs",
         expected_stem="nested/result", expected_exit=0, main=True, project_opt_in=False,
         diagnostic=None, project_subdir=False):
    work = root / name
    work.mkdir()
    project = work / "project" if project_subdir else work
    project.mkdir(exist_ok=True)
    source = 'method {:test} Check() { expect true; }\n'
    if main and command != "test":
        source += 'method Main() { print "main\\n"; }\n'
    (project / "foo.dfy").write_text(source)
    project_options = "output = " + json.dumps(project_output) + "\n"
    if project_opt_in:
        project_options += "project-output = true\n"
    (project / "dfyconfig.toml").write_text('includes = ["foo.dfy"]\n[options]\n' + project_options)
    args = launcher + [command]
    if command == "translate":
        args.append(target)
    args += ["project/dfyconfig.toml" if project_subdir else "dfyconfig.toml", "--no-verify",
             "--type-system-refresh:" + mode]
    if command != "translate":
        args += ["--spill-translation", "--target", target]
    args += list(extra)
    result = subprocess.run(args, cwd=work, capture_output=True, text=True, timeout=90)
    files = sorted(str(p.relative_to(work)).replace(os.sep, "/") for p in work.rglob("*") if p.is_file())
    records.append({"name": name, "exit": result.returncode, "stdout": result.stdout,
                    "stderr": result.stderr, "files": files})
    errors = []
    if result.returncode != expected_exit:
        errors.append("exit " + str(result.returncode) + ", expected " + str(expected_exit))
    output = result.stdout + result.stderr
    if diagnostic and diagnostic not in output:
        errors.append("missing diagnostic: " + diagnostic)
    if expected_stem is not None:
        expected = expected_stem + {"cs": ".cs", "py": "-py/__main__.py", "js": ".js"}[target]
        if expected not in files:
            errors.append("missing target: " + expected)
        if target == "cs" and command != "translate" and expected_stem + ".dll" not in files:
            errors.append("missing compiled assembly")
        if expected_stem != "foo" and any(f in files for f in ["foo.cs", "foo.dll", "foo.js", "foo-py/__main__.py"]):
            errors.append("used source filename instead of output setting")
        if command == "run" and main and "main\n" not in result.stdout:
            errors.append("Main was not run")
        if command == "test" and "PASSED" not in result.stdout:
            errors.append("test runner did not pass")
    elif any(f.endswith((".cs", ".csproj", ".dll", ".js", ".py")) for f in files):
        errors.append("created compiler files for invalid output")
    if errors:
        failures.append((name, errors, output))
        print("FAIL " + name + ": " + "; ".join(errors))
    else:
        print("PASS " + name)

for mode in ["false", "true"]:
    for command in ["build", "run", "test"]:
        enable = ["--project-output"] if command == "run" else []
        prefix = mode + "-" + command
        case(prefix + "-project", command, mode, extra=enable)
        case(prefix + "-bin-filename", command, mode, project_output="bin", extra=enable, expected_stem="bin")
        case(prefix + "-override", command, mode, extra=enable + ["--output", "chosen"], expected_stem="chosen")
        case(prefix + "-slash", command, mode, project_output="bin/", extra=enable,
             expected_stem=None, expected_exit=3, diagnostic="Invalid output filename")
        case(prefix + "-empty-stem", command, mode, extra=enable + ["--output", ".cs"],
             expected_stem=None, expected_exit=3, diagnostic="Invalid output filename")
        case(prefix + "-empty-project", command, mode, project_output="", extra=enable,
             expected_stem=None, expected_exit=3, diagnostic="Invalid output filename")
        case(prefix + "-invalid-project", command, mode, project_output="\0", extra=enable,
             expected_stem=None, expected_exit=1, diagnostic="Invalid value for option output")
        case(prefix + "-invalid-project-override", command, mode, project_output="\0", extra=enable + ["--output", "chosen"], expected_stem="chosen")
        case(prefix + "-empty-project-override", command, mode, project_output="", extra=enable + ["--output", "chosen"], expected_stem="chosen")
    for target in ["java", "go", "cpp", "rs", "js", "lib"]:
        case(mode + "-" + target + "-slash", "build", mode, target=target, project_output="bin/", extra=["--project-output"],
             expected_stem=None, expected_exit=3, diagnostic="Invalid output filename")
    case(mode + "-translate-cs-default", "translate", mode, project_output="bin/", expected_stem="bin/")
    case(mode + "-build-library", "build", mode, main=False)
    case(mode + "-run-library", "run", mode, main=False, extra=["--project-output"])
    case(mode + "-run-project-relative", "run", mode, project_subdir=True, extra=["--project-output"], expected_stem="project/nested/result")
    case(mode + "-run-cli-relative", "run", mode, project_subdir=True, extra=["--project-output", "--output", "chosen"], expected_stem="chosen")
    case(mode + "-run-spaces", "run", mode, project_output="folder with spaces/result", extra=["--project-output"], expected_stem="folder with spaces/result")
    case(mode + "-run-default", "run", mode, project_output="bin/", expected_stem="foo")
    case(mode + "-run-cli", "run", mode, extra=["--output", "chosen"], expected_stem="chosen")
    case(mode + "-run-project-option", "run", mode, project_opt_in=True)
    case(mode + "-run-disabled", "run", mode, project_opt_in=True, extra=["--project-output:false"], expected_stem="foo")
    case(mode + "-run-build-alias", "run", mode, extra=["--build", "chosen"], expected_stem="chosen")
    case(mode + "-translate-js-default", "translate", mode, target="js", project_output="bin/", expected_stem="bin/")
    for target in ["py"]:
        case(mode + "-" + target + "-default-slash", "build", mode, target=target, project_output="bin/", expected_stem="bin/")
        for command in ["build", "run", "test"]:
            case(mode + "-" + target + "-" + command, command, mode, target=target, extra=["--project-output"])
        case(mode + "-" + target + "-slash", "run", mode, target=target, project_output="bin/", extra=["--project-output"],
             expected_stem=None, expected_exit=3, diagnostic="Invalid output filename")

(root / "results.json").write_text(json.dumps(records, indent=2))
if failures:
    for name, errors, output in failures:
        print(name + "\n" + output, file=sys.stderr)
    if not record_only:
        sys.exit(1)
# Scratch probes record expected failures without failing the workflow.
