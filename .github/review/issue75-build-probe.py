"""Safe exploratory migration checks; record failures and always exit zero."""
import json
import os
from pathlib import Path
import subprocess

out = Path("issue75-probe")
out.mkdir(exist_ok=True)
records = []
def run(name, command, timeout=1800):
    try:
        result = subprocess.run(command, text=True, capture_output=True, timeout=timeout)
        status, text = result.returncode, result.stdout + result.stderr
    except subprocess.TimeoutExpired as error:
        status, text = "TIMEOUT", str(error)
    (out / (name + ".txt")).write_text(text)
    records.append({"name": name, "status": status, "command": command})
    print(name, status, flush=True)
    return status == 0

try:
    run("inventory", ["dotnet", "--info"])
    run("boogie", ["sh", "Scripts/fetch-boogie-packages.sh"])
    if run("build", ["dotnet", "build", "Source/Dafny.sln", "-c", "Release"]):
        run("core-unit", ["dotnet", "test", "Source/DafnyCore.Test", "-c", "Release", "--no-build"])
        run("runtime-unit", ["dotnet", "test", "Source/DafnyRuntime.Tests", "-c", "Release", "--no-build"])
        run("smoke", ["dotnet", "test", "Source/IntegrationTests", "-c", "Release", "--no-build",
                      "--filter", "DisplayName~github-issue-75.dfy|DisplayName~github-issue-104.dfy"])
except Exception as error:
    records.append({"name": "probe exception", "status": str(error)})
finally:
    (out / "summary.json").write_text(json.dumps(records, indent=2))
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a") as stream:
            stream.write("## .NET 10 migration probe\n\n" + "\n".join(str(row) for row in records) + "\n")
