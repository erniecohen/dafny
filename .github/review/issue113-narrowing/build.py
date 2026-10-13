import hashlib
import json
import pathlib
import subprocess
import tarfile

SOURCE = "acf2bf00f824f6c7a68f6c6293e6ddc65f1dce2f"
out = pathlib.Path("observations")
out.mkdir(exist_ok=True)
report = {"source": SOURCE, "classification": "exact published development source build; no proof calls", "steps": [], "build_completed": False}

def run(label, command):
    result = subprocess.run(command, capture_output=True, text=True)
    (out / (label + ".stdout.txt")).write_text(result.stdout)
    (out / (label + ".stderr.txt")).write_text(result.stderr)
    row = {"label": label, "command": command, "exit": result.returncode}
    report["steps"].append(row)
    (out / "report.json").write_text(json.dumps(report, indent=2) + "\n")
    print(label, result.returncode, flush=True)
    return result

try:
    run("clone", ["git", "clone", "--filter=blob:none", "--no-checkout", "https://github.com/erniecohen/dafny.git", "candidate"])
    checkout = run("checkout", ["git", "-C", "candidate", "checkout", "--detach", SOURCE])
    if checkout.returncode != 0:
        raise RuntimeError("exact checkout failed")
    actual = run("identity", ["git", "-C", "candidate", "rev-parse", "HEAD"])
    assert actual.stdout.strip() == SOURCE
    script = pathlib.Path("candidate/Scripts/fetch-boogie-packages.sh")
    if script.exists():
        fetched = run("fetch-packages", ["sh", "-c", "cd candidate && sh Scripts/fetch-boogie-packages.sh"])
        assert fetched.returncode == 0
    build = run("publish", ["dotnet", "publish", "candidate/Source/Dafny/Dafny.csproj", "-c", "Release", "-o", str(out / "dafny"), "-p:SourceRevisionId=" + SOURCE[:8]])
    if build.returncode == 0:
        version = run("version", ["dotnet", str(out / "dafny/Dafny.dll"), "--version"])
        report["reported_version"] = version.stdout.strip()
        assert version.returncode == 0 and version.stdout.strip() == "4.11.1+" + SOURCE[:8]
        with tarfile.open(out / "dafny.tar.gz", "w:gz") as archive:
            archive.add(out / "dafny", arcname="dafny")
        report["archive_sha256"] = hashlib.sha256((out / "dafny.tar.gz").read_bytes()).hexdigest()
        report["build_completed"] = True
except Exception as error:
    report["infrastructure_or_build_error"] = str(error)
finally:
    (out / "report.json").write_text(json.dumps(report, indent=2) + "\n")
    summary = "Exact source: `" + SOURCE + "`. Build completed: `" + str(report["build_completed"]) + "`. No proofs executed. Diagnostic status is separate from build acceptance.\n"
    import os
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as target:
        target.write(summary)
