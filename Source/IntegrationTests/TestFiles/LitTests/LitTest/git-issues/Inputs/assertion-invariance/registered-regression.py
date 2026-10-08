"""Run the original reproducer, direct repairs, and non-vacuity controls."""
from pathlib import Path
import subprocess
import sys

assembly=Path(sys.argv[1]).resolve()
solver=Path(sys.argv[2]).resolve()
work=Path(sys.argv[3]).resolve()
work.mkdir(parents=True,exist_ok=True)
fixtures=Path(__file__).resolve().parent
assert "5.1.0" in subprocess.check_output([str(solver),"--version"],text=True)
help_result=subprocess.run(["dotnet",str(assembly),"verify","--help"],text=True,capture_output=True)
assert "--consistent-obligation-checks" in help_result.stdout
print("PASS CLI option help")

def verify(name,refresh=False,axioms=False,enabled=True,project=False,override=None,isolate=False):
 source=work/"dfyconfig.toml" if project else fixtures/(name+".dfy")
 command=["dotnet",str(assembly),"verify",str(source),"--solver-path",str(solver),
  "--cores","1","--resource-limit","16000000","--verification-time-limit","30",
  f"--type-system-refresh:{str(refresh).lower()}",f"--general-newtypes:{str(refresh).lower()}",
  f"--additional-axioms:{str(axioms).lower()}","--boogie","/normalizeDeclarationOrder:0"]
 if not project or override is not None:
  command.append(f"--consistent-obligation-checks:{str(enabled if override is None else override).lower()}")
 if name in ["certified-spec-only-reads","negative-certified-conditional-reads"]:
  command.append("--reads-clauses-on-methods")
 if isolate: command.append("--isolate-assertions")
 result=subprocess.run(command,text=True,capture_output=True,timeout=120)
 output=result.stdout+result.stderr
 assert not any(t in output.lower() for t in ["out of resource","timed out","internal error","unhandled exception"]),output
 return result.returncode,output

for refresh in [False,True]:
 for axioms in [False,True]:
  for name in ["issue100-original","issue100-explicit","issue100-final-true"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
print("PASS original issue 100 at unchanged resource limit")
for refresh in [False,True]:
 for axioms in [False,True]:
  for name in ["subset-short","subset-recursive","subset-assignment","subset-cast","generic-subset","nested-subset"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
print("PASS implicit subset constraints")
for refresh in [False,True]:
 for axioms in [False,True]:
  for name in ["sequence-constraint","sequence-empty","array-initializer"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  for name in ["negative-sequence-constraint","negative-array-initializer"]:
   code,output=verify(name,refresh,axioms)
   assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS range-bound collection constraints")
for refresh in [False,True]:
 for axioms in [False,True]:
  for enabled in [False,True]:
   code,output=verify("terminal-reveal-scope",refresh,axioms,enabled=enabled)
   assert code==0,output
print("PASS legacy terminal reveal scopes")
for refresh in [False,True]:
 for axioms in [False,True]:
  for name in ["negative-subset","negative-guarded-constraint","negative-subset-vacuity",
    "negative-self-postcondition","negative-self-postcondition-let","negative-allocation-vacuity",
    "negative-old-argument","negative-forall","negative-higher-precondition","negative-ordered-contract",
    "negative-proof-reveal","negative-hidden-after-reveal",
    "negative-boolean-value-argument","negative-boolean-value-higher-order",
    "negative-boolean-value-container","negative-boolean-value-let","negative-boolean-value-membership",
    "nonvacuity-boolean-value-argument","nonvacuity-boolean-value-higher-order",
    "nonvacuity-boolean-value-container","nonvacuity-boolean-value-let","nonvacuity-boolean-value-membership"]:
   for isolate in [False,True]:
    code,output=verify(name,refresh,axioms,isolate=isolate)
    assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS false controls")
for refresh in [False,True]:
 for axioms in [False,True]:
  for name in ["certified-contract-domain","certified-spec-only-reads"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  for name in ["negative-certified-contract-domain","negative-certified-first-postcondition",
    "negative-certified-conditional-reads"]:
   for isolate in [False,True]:
    code,output=verify(name,refresh,axioms,isolate=isolate)
    assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS certified contract preparation and false controls")
(work/"subset.dfy").write_text((fixtures/"subset-short.dfy").read_text())
(work/"dfyconfig.toml").write_text("[options]\nconsistent-obligation-checks = true\n")
code,output=verify("subset-short",project=True)
assert code==0,output
code,output=verify("subset-short",project=True,override=False)
assert code==4 and "subset" in output,output
print("PASS project option and CLI precedence")
