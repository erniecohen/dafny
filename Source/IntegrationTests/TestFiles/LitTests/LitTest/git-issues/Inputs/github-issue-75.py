"""The .NET 10 fork must retain the C# backend's .NET 8 output target."""
import json
from pathlib import Path
import subprocess
import sys

assembly = Path(sys.argv[1]).resolve()
work = Path(sys.argv[2]).resolve()
work.mkdir(parents=True, exist_ok=True)
config = json.loads(assembly.with_suffix(".runtimeconfig.json").read_text())
assert config["runtimeOptions"]["tfm"] == "net10.0", config
print("PASS fork framework")
(work / "main.dfy").write_text('method Main() { print "generated execution\\n"; }\n')
result = subprocess.run(["dotnet", str(assembly), "run", "main.dfy", "--no-verify",
                         "--spill-translation"], cwd=work, text=True, capture_output=True, timeout=120)
assert result.returncode == 0, result.stdout + result.stderr
assert "<TargetFramework>net8.0</TargetFramework>" in (work / "main.csproj").read_text()
config = json.loads((work / "main.runtimeconfig.json").read_text())
assert config["runtimeOptions"]["tfm"] == "net8.0", config
print("PASS generated framework")
assert "generated execution\n" in result.stdout, result.stdout + result.stderr
print("PASS generated execution")
