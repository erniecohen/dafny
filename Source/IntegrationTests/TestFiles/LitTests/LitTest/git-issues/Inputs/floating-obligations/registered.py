"""Require inhabited float constraints and real Invalid controls in both lowering modes."""
from pathlib import Path
import json, subprocess, sys
assembly, solver, work = map(lambda p: Path(p).resolve(), sys.argv[1:4])
work.mkdir(parents=True, exist_ok=True)
assert "5.1.0" in subprocess.check_output([str(solver), "--version"], text=True)
for refresh in [False, True]:
 for enabled in [False, True]:
  for name in ["positive", "negative"]:
   dest = work / f"{name}-{refresh}-{enabled}"
   dest.mkdir(exist_ok=True)
   command = ["dotnet", str(assembly), "verify", str(Path(__file__).with_name(name + ".dfy")),
    "--solver-path", str(solver), "--cores", "1", "--resource-limit", "16000000",
    "--verification-time-limit", "30", f"--type-system-refresh:{str(refresh).lower()}",
    f"--general-newtypes:{str(refresh).lower()}", f"--consistent-obligation-checks:{str(enabled).lower()}",
    "--additional-axioms:false", "--boogie", "/normalizeDeclarationOrder:0",
    "--log-format", "json;LogFileName=" + str(dest / "results.json")]
   (dest / "command.json").write_text(json.dumps(command))
   result = subprocess.run(command, capture_output=True, text=True, timeout=180)
   output = result.stdout + result.stderr
   (dest / "output.txt").write_text(output)
   assert result.returncode == (0 if name == "positive" else 4), output
   declarations = json.loads((dest / "results.json").read_text())["verificationResults"]
   assert declarations, output
   expected_correctness = {n + " (correctness)" for n in (["Inhabited32", "Inhabited64", "Preserve32", "Preserve64", "SignedZero32", "SignedZero64"] if name == "positive" else ["Invalid32", "Invalid64", "False32", "False64", "WrongSign32", "WrongSign64"])}
   assert {d["name"] for d in declarations if d["name"].endswith(" (correctness)")} == expected_correctness, output
   expected_wrapper_wf = {n + " (well-formedness)" for n in ["NonNaN32", "NonNaN64", "Finite32", "Finite64"]}
   assert expected_wrapper_wf <= {d["name"] for d in declarations if d["outcome"] == "Correct"}, output
   expected_errors = {n + " (correctness)" for n in ["Invalid32", "Invalid64", "False32", "False64", "WrongSign32", "WrongSign64"]} if name == "negative" else set()
   assert {d["name"] for d in declarations if d["outcome"] == "Errors"} == expected_errors, output
   assert all(d["outcome"] in ["Correct", "Errors"] for d in declarations), output
   for d in declarations:
    outcomes = [v["outcome"] for v in d["vcResults"]]
    assert outcomes and all(v in ["Valid", "Invalid"] for v in outcomes), output
    assert ("Invalid" in outcomes) if d["name"] in expected_errors else all(v == "Valid" for v in outcomes), output
print("PASS inhabited float constraints, invalid NaN introductions, and feasible false controls in both resolvers and OFF/ON")
