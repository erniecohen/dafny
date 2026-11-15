#!/usr/bin/env python3
"""Capture bounded obligation comparisons; expected failures are data, not green checks."""
import argparse, csv, hashlib, json, pathlib, subprocess
parser=argparse.ArgumentParser()
parser.add_argument("dafny",type=pathlib.Path)
parser.add_argument("solver",type=pathlib.Path)
parser.add_argument("output",type=pathlib.Path)
parser.add_argument("--shipped",action="store_true")
parser.add_argument("--all-resolvers",action="store_true")
parser.add_argument("--seeds",default="0")
parser.add_argument("--cases",default="")
args=parser.parse_args()
exe=args.dafny.resolve(); solver=args.solver.resolve(); output=args.output.resolve()
output.mkdir(parents=True,exist_ok=True)
root=pathlib.Path("Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/assertion-invariance")
def capture(command):
 return subprocess.run(command,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
report={"commit":capture(["git","rev-parse","HEAD"]).stdout.strip(),
 "dafny":capture([str(exe),"--version"]).stdout.strip(),
 "solver":capture([str(solver),"--version"]).stdout.strip(),
 "solver_sha256":hashlib.sha256(solver.read_bytes()).hexdigest(),"cases":[]}
cases=json.loads((root/"cases.json").read_text())
for case in cases:
 if args.cases and case["name"] not in args.cases.split(","): continue
 source=root/(case["name"]+".dfy")
 for refresh in ([False,True] if args.all_resolvers else [False]):
  for axioms in ([False,True] if args.shipped else [False]):
   for enabled in [False,True]:
    for seed in map(int,args.seeds.split(",")):
     name=f"{source.stem}-refresh-{refresh}-axioms-{axioms}-enabled-{enabled}-seed-{seed}"
     directory=output/name; directory.mkdir(exist_ok=True)
     command=[str(exe),"verify",str(source),"--solver-path",str(solver),"--cores","1",
       "--resource-limit","16000000","--verification-time-limit","30",
       f"--type-system-refresh:{str(refresh).lower()}", f"--general-newtypes:{str(refresh).lower()}",
       f"--consistent-obligation-checks:{str(enabled).lower()}",
       "--boogie","/normalizeDeclarationOrder:0","--boogie",f"/randomSeed:{seed}",
       "--bprint",str(directory/"program.bpl"),
       "--log-format",f"csv;LogFileName={directory/'resources.csv'}"]
     command.extend(case.get("options", []))
     if source.stem in ["issue100-original","issue100-explicit","issue100-final-true","subset-short","subset-short-explicit","negative-self-postcondition-let"]:
      command.extend(["--solver-log",str(directory/"solver.smt2"),"--pprint",str(directory/"passive.bpl")])
     if args.shipped: command.append(f"--additional-axioms:{str(axioms).lower()}")
     result=capture(command)
     (directory/"output.txt").write_text(result.stdout)
     batches=[]
     if (directory/"resources.csv").exists():
      with (directory/"resources.csv").open() as f: batches=list(csv.DictReader(f))
     resources=[int(row.get("TestResult.ResourceCount","0") or "0") for row in batches]
     outcomes=sorted(set(row.get("TestResult.Outcome","") for row in batches))
     row={"name":name,"case":case,"refresh":refresh,"axioms":axioms,"enabled":enabled,"seed":seed,
      "source_sha256":hashlib.sha256(source.read_bytes()).hexdigest(),"command":command,
      "exit":result.returncode,"total_ru":sum(resources),"max_batch_ru":max(resources,default=0),
      "outcomes":outcomes,"summary":[line for line in result.stdout.splitlines() if "Dafny program verifier" in line]}
     # A rejected negative is useful only when proof checks fail, not parsing, timeout or resource exhaustion.
     expected=case.get("expected",0)
     row["accepted_expectation"]=result.returncode==expected and not any(
       word in result.stdout.lower() for word in ["out of resource","timed out","internal error","unhandled exception"])
     (directory/"result.json").write_text(json.dumps(row,indent=2))
     report["cases"].append(row)
     print(name,result.returncode,row["total_ru"],row["summary"],flush=True)
(output/"summary.json").write_text(json.dumps(report,indent=2))
