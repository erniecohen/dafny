"""Run the original reproducer, direct repairs, and non-vacuity controls."""
from pathlib import Path
import json
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
  "--boogie","/normalizeDeclarationOrder:0"]
 if not project or override is not None:
  command.append(f"--consistent-obligation-checks:{str(enabled if override is None else override).lower()}")
 if name in ["certified-spec-only-reads","negative-certified-conditional-reads"]:
  command.append("--reads-clauses-on-methods")
 if isolate: command.append("--isolate-assertions")
 replay=name in ["negative-twostate-labeled-replay","twostate-labeled-recursive",
  "negative-twostate-nested-replay","twostate-nested-replay"]
 logged=replay or name=="issue100-original"
 if logged:
  replay_log=work/f"{name}-refresh-{refresh}-axioms-{axioms}-enabled-{enabled}-isolate-{isolate}.json"
  command.extend(["--log-format","json;LogFileName="+str(replay_log)])
 result=subprocess.run(command,text=True,capture_output=True,timeout=120)
 output=result.stdout+result.stderr
 forbidden=["timed out","internal error","unhandled exception"]
 if name!="issue100-original": forbidden.append("out of resource")
 assert not any(t in output.lower() for t in forbidden),output
 if name=="issue100-original":
  # Native dev baseline 858e4bf and candidate receipts: runs38050879264 and38051337781.
  # This is a visible performance exception, never a successful proof receipt.
  declarations=json.loads(replay_log.read_text())["verificationResults"]
  expected={"iChain (well-formedness)":"Correct","iRegress (well-formedness)":"Correct",
   "rf (well-formedness)":"Correct","wfi (correctness)":"OutOfResource"}
  assert {d["name"]:d["outcome"] for d in declarations}==expected,output
  for declaration in declarations:
   outcomes=[vc["outcome"] for vc in declaration["vcResults"]]
   assert outcomes and all(outcome==("OutOfResource" if declaration["name"]=="wfi (correctness)" else "Valid") for outcome in outcomes),output
  assert result.returncode==4 and "out of resource" in output.lower(),output
 if replay:
  declarations=json.loads(replay_log.read_text())["verificationResults"]
  if name=="negative-twostate-labeled-replay":
   expected={"ReplayFalse (well-formedness)":"Correct",
    "ReplayCallee (well-formedness)":"Correct","ReplayCaller (well-formedness)":"Correct",
    "ReplayCaller (correctness)":"Errors","DirectFalse (well-formedness)":"Correct",
    "DirectFalse (correctness)":"Errors","ReachReplayCaller (correctness)":"Correct"}
  elif name=="negative-twostate-nested-replay":
   expected={"ReplayHelper (well-formedness)":"Correct","ReplayHelper (correctness)":"Correct",
    "ReplayCallee (well-formedness)":"Correct","ReplayCaller (well-formedness)":"Correct",
    "ReplayCaller (correctness)":"Errors","DirectFalse (well-formedness)":"Correct",
    "DirectFalse (correctness)":"Errors","ReachReplayCaller (correctness)":"Correct"}
  elif name=="twostate-nested-replay":
   expected={"ReplayHelper (well-formedness)":"Correct","ReplayHelper (correctness)":"Correct",
    "ReplayCallee (well-formedness)":"Correct","ReplayCaller (well-formedness)":"Correct",
    "ReplayCaller (correctness)":"Correct","ReachReplayCaller (correctness)":"Correct"}
  else:
   expected={"ReplayValue (well-formedness)":"Correct",
    "ReplayValueCallee (well-formedness)":"Correct","ReplayValueCaller (well-formedness)":"Correct",
    "ReplayValueCaller (correctness)":"Correct","ReachReplayValueCaller (correctness)":"Correct"}
  # OFF retains dev's existing labeled-callee termination failure; ON must repair it.
  # Independent dev baseline receipt: run38051337781, both resolvers, unchanged fixtures.
  if not enabled and name=="twostate-labeled-recursive": expected["ReplayValueCaller (correctness)"]="Errors"
  if not enabled and name=="twostate-nested-replay": expected["ReplayCaller (correctness)"]="Errors"
  assert {d["name"]:d["outcome"] for d in declarations}==expected,output
  for declaration in declarations:
   outcomes=[vc["outcome"] for vc in declaration["vcResults"]]
   assert outcomes and all(outcome in ["Valid","Invalid"] for outcome in outcomes),output
   assert ("Invalid" in outcomes) if expected[declaration["name"]]=="Errors" else all(
    outcome=="Valid" for outcome in outcomes),output
 return result.returncode,output

for refresh in [False,True]:
 for axioms in [False]:
  for name in ["issue100-original","issue100-explicit","issue100-final-true"]:
   code,output=verify(name,refresh,axioms)
   assert code==(4 if name=="issue100-original" else 0),output
print("KNOWN LIMIT native dev original issue 100: wfi OutOfResource at unchanged ceiling; three supporting definitions Correct")
print("PASS original issue 100 explicit and final-true controls at unchanged resource limit")
for refresh in [False,True]:
 for axioms in [False]:
  for name in ["subset-short","subset-recursive","subset-assignment","subset-cast","generic-subset","nested-subset"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
print("PASS implicit subset constraints")
for refresh in [False,True]:
 for axioms in [False]:
  for name in ["sequence-constraint","sequence-empty","array-initializer"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  for name in ["negative-sequence-constraint","negative-array-initializer"]:
   code,output=verify(name,refresh,axioms)
   assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS range-bound collection constraints")
for refresh in [False,True]:
 for axioms in [False]:
  for enabled in [False,True]:
   code,output=verify("terminal-reveal-scope",refresh,axioms,enabled=enabled)
   assert code==0,output
print("PASS legacy terminal reveal scopes")
for refresh in [False,True]:
 for axioms in [False]:
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
 for axioms in [False]:
  for name in ["certified-contract-domain","certified-spec-only-reads"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  for name in ["negative-certified-contract-domain","negative-certified-first-postcondition",
    "negative-certified-conditional-reads","negative-call-scope-publication"]:
   for isolate in [False,True]:
    code,output=verify(name,refresh,axioms,isolate=isolate)
    assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS certified contract preparation and false controls")
for refresh in [False,True]:
 for axioms in [False]:
  for name in ["certified-guarded-arguments","certified-guarded-arguments-explicit"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  for isolate in [False,True]:
   code,output=verify("negative-certified-guarded-arguments",refresh,axioms,isolate=isolate)
   assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS guarded private argument preparation")
for refresh in [False,True]:
 for axioms in [False]:
  for name in ["caller-quantified-wf","caller-quantified-wf-explicit"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  code,output=verify("negative-caller-quantified-wf",refresh,axioms)
  assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS canonical caller wellformedness preparation")
for refresh in [False,True]:
 for axioms in [False]:
  for name in ["exit-quantified-wf","exit-quantified-wf-explicit"]:
   code,output=verify(name,refresh,axioms)
   assert code==0,output
  code,output=verify("negative-exit-quantified-wf",refresh,axioms)
  assert code==4 and "error" in output and "parse errors" not in output,output
print("PASS canonical local exit wellformedness preparation")
# This empty-body graph lemma needs earlier proved postconditions to supply
# the concrete initial-heap path for the final existential postcondition.
for refresh in [False,True]:
 for axioms in [False]:
  code,output=verify("schorr-initial-path",refresh,axioms)
  assert code==0,output
  code,output=verify("schorr-initial-path",refresh,axioms,enabled=False)
  source=fixtures/"schorr-initial-path.dfy"
  line=next(i for i,text in enumerate(source.read_text().splitlines(),1)
    if text.strip()=="ensures old(Reachable(root, child, S))")
  assert code==4 and f"schorr-initial-path.dfy({line}," in output and "1 error" in output,output
  for enabled in [False,True]:
   code,output=verify("schorr-initial-path-explicit",refresh,axioms,enabled=enabled)
   assert code==0,output
   code,output=verify("negative-schorr-initial-edge",refresh,axioms,enabled=enabled)
   source=fixtures/"negative-schorr-initial-edge.dfy"
   line=next(i for i,text in enumerate(source.read_text().splitlines(),1)
     if text.strip()=="ensures old(ReachableVia(root, Path.Extend(prefix, parent), child, S))")
   assert code==4 and f"negative-schorr-initial-edge.dfy({line}," in output,output
   code,output=verify("schorr-initial-path-nonvacuity",refresh,axioms,enabled=enabled)
   source=fixtures/"schorr-initial-path-nonvacuity.dfy"
   line=next(i for i,text in enumerate(source.read_text().splitlines(),1)
     if "assert false;" in text)
   assert code==4 and f"schorr-initial-path-nonvacuity.dfy({line}," in output and "1 error" in output,output
print("PASS sequential initial-heap path postconditions and false controls")
# A second Invalid declaration must not mask unsound acceptance of the caller.
# Check every declaration above and both independently failing source locations.
for refresh in [False,True]:
 for axioms in [False]:
  for enabled in [False,True]:
   code,output=verify("twostate-labeled-recursive",refresh,axioms,enabled=enabled)
   assert code==(0 if enabled else 4),output
   code,output=verify("negative-twostate-labeled-replay",refresh,axioms,enabled=enabled)
   source=fixtures/"negative-twostate-labeled-replay.dfy"
   lines=source.read_text().splitlines()
   call_line=next(i for i,text in enumerate(lines,1) if "ReplayCallee@ReplayCurrent(c);" in text)
   false_line=next(i for i,text in enumerate(lines,1) if "Independent invalid assertion" in text)
   assert code==4 and f"negative-twostate-labeled-replay.dfy({call_line}," in output,output
   assert f"negative-twostate-labeled-replay.dfy({false_line}," in output,output
print("PASS labeled recursive two-state requires and independent false controls")
for refresh in [False,True]:
 for axioms in [False]:
  for enabled in [False,True]:
   code,output=verify("twostate-nested-replay",refresh,axioms,enabled=enabled)
   assert code==(0 if enabled else 4),output
   code,output=verify("negative-twostate-nested-replay",refresh,axioms,enabled=enabled)
   source=fixtures/"negative-twostate-nested-replay.dfy"
   lines=source.read_text().splitlines()
   call_line=next(i for i,text in enumerate(lines,1) if "ReplayCallee@ReplayCurrent(c);" in text)
   false_line=next(i for i,text in enumerate(lines,1) if "Independent invalid assertion" in text)
   assert code==4 and f"negative-twostate-nested-replay.dfy({call_line}," in output,output
   assert f"negative-twostate-nested-replay.dfy({false_line}," in output,output
print("PASS nested two-state statement expressions and independent false controls")

(work/"subset.dfy").write_text((fixtures/"subset-short.dfy").read_text())
(work/"dfyconfig.toml").write_text("[options]\nconsistent-obligation-checks = true\n")
code,output=verify("subset-short",project=True)
assert code==0,output
code,output=verify("subset-short",project=True,override=False)
assert code==4 and "subset" in output,output
print("PASS project option and CLI precedence")
