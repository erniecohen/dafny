#!/usr/bin/env python3
"""Six-pair diagnostic capture using existing bundles; no builds or verdict waivers."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = Path.cwd()
PINS = json.loads((HERE / "pins.json").read_text())
COLLECTOR = ROOT / ".github/review/issue113-default-off"
STATUS = ROOT / "run-metadata"
SETUP = ROOT / "setup"

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")

def recorded(cmd, name, stdout=None):
    STATUS.mkdir(parents=True, exist_ok=True)
    out = stdout or STATUS / (name + ".stdout")
    err = STATUS / (name + ".stderr")
    with out.open("wb") as output, err.open("wb") as errors:
        result = subprocess.run(cmd, stdout=output, stderr=errors)
    dump(STATUS / (name + ".command.json"), {"argv":cmd, "exit":result.returncode})
    if result.returncode:
        raise RuntimeError(name + " failed with actual exit " + str(result.returncode))

def modules():
    sys.path.insert(0, str(COLLECTOR))
    import prepare
    return prepare

def frozen():
    for name, expected in PINS["frozen_collector_files"].items():
        if digest(ROOT / name) != expected:
            raise ValueError("Frozen helper/spec changed: " + name)

def component_snapshot(line, phase):
    prepare = modules()
    result = {"line":line, "phase":phase, "solver_sha256":digest(Path(os.environ["SOLVER"])), "arms":{}}
    if result["solver_sha256"] != PINS["solver"]["executable_sha256"]:
        raise ValueError("Actual named solver bytes differ")
    for side in ("baseline","final"):
        identity = json.loads((ROOT / "compilers" / side / "compiler-components.json").read_text())
        executable = ROOT / "out" / side / "dafny/Dafny"
        actual = prepare.components(executable)
        libs = {f.relative_to(executable.parent).as_posix():digest(f) for f in sorted(executable.parent.rglob("*.doo"))}
        if actual != identity["components_sha256"] or libs != identity["bundled_libraries_sha256"]:
            raise ValueError("Actual compiler/library bytes differ: " + side)
        product = PINS["sources"][line]["repaired_product" if side=="baseline" else "final_product"]
        if identity["line"] != line or identity["side"] != side or identity["product_revision"] != product:
            raise ValueError("Wrong reused compiler product")
        result["arms"][side] = {"product_revision":product, "components_sha256":actual,
                                "bundled_libraries_sha256":libs,
                                "reused_archive_sha256":identity["archive_sha256"]}
    dump(STATUS / ("components-" + phase + ".json"), result)
    return result

def setup(line):
    frozen()
    SETUP.mkdir(exist_ok=True)
    prepare = modules()
    dump(SETUP / "original-inputs-before.json", prepare.inputs(line, ROOT / "inputs"))
    artifact = PINS["compiler_artifacts"][line]
    metadata_file = SETUP / "producer-artifact.json"
    api = "repos/" + PINS["repository"] + "/actions/artifacts/" + str(artifact["id"])
    recorded(["gh","api",api], "artifact-metadata", metadata_file)
    actual = json.loads(metadata_file.read_text())
    if (actual["id"],actual["name"],actual["size_in_bytes"],actual["digest"]) != (artifact["id"],artifact["name"],artifact["bytes"],"sha256:"+artifact["sha256"]):
        raise ValueError("Producer artifact identity changed")
    if actual["expired"] or actual["workflow_run"]["id"] != PINS["producer_run"] or actual["workflow_run"]["head_sha"] != PINS["producer_head"]:
        raise ValueError("Unavailable or wrong producer run")
    archive = SETUP / "built-compilers.zip"
    recorded(["gh","api",api+"/zip"],"download-built-compilers",archive)
    if archive.stat().st_size != artifact["bytes"] or digest(archive) != artifact["sha256"]:
        raise ValueError("Actual compiler ZIP bytes differ from public digest")
    compilers = ROOT / "compilers"; compilers.mkdir()
    with zipfile.ZipFile(archive) as zip_:
        for name in zip_.namelist():
            relative=Path(name)
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Unexpected compiler ZIP path")
        zip_.extractall(compilers)
    if (compilers / "harness-source.txt").read_text().strip() != PINS["producer_head"]:
        raise ValueError("Compiler producer collector head differs")
    for side in ("baseline","final"):
        for kind in ("build","identity","compiler-version","dependencies"):
            if (compilers / side / (kind+"-exit.txt")).read_text().strip() != "0":
                raise ValueError("Producer build/setup was incomplete")
        identity=json.loads((compilers / side / "compiler-components.json").read_text())
        tarpath=compilers / side / "dafny.tar.gz"
        if digest(tarpath) != identity["archive_sha256"]:
            raise ValueError("Compiler TAR bytes differ")
        destination=ROOT / "out" / side; destination.mkdir(parents=True)
        with tarfile.open(tarpath,"r:gz") as tar:
            if any(not m.name.startswith("dafny") or m.issym() or m.islnk() or ".." in Path(m.name).parts for m in tar.getmembers()):
                raise ValueError("Unexpected compiler TAR path/link")
            tar.extractall(destination, filter="data")
    solver=PINS["solver"]; archive=SETUP / "z3-5.1.0-x64-glibc-2.39.zip"
    recorded(["curl","--retry","3","--retry-all-errors","-fsSLo",str(archive),solver["url"]],"download-named-z3")
    if digest(archive) != solver["sha256"]:
        raise ValueError("Pinned Z3 archive checksum differs")
    destination=ROOT / "solver"; destination.mkdir()
    with zipfile.ZipFile(archive) as zip_:
        if any(Path(n).is_absolute() or ".." in Path(n).parts for n in zip_.namelist()):
            raise ValueError("Unexpected solver ZIP path")
        zip_.extractall(destination)
    executable=destination / "z3-5.1.0-x64-glibc-2.39/bin/z3"
    executable.chmod(executable.stat().st_mode | 0o111)
    if digest(executable) != solver["executable_sha256"]:
        raise ValueError("Named solver executable bytes differ")
    recorded([str(executable),"--version"],"solver-version",SETUP / "solver-version.txt")
    if (SETUP / "solver-version.txt").read_text().strip() != "Z3 version 5.1.0 - 64 bit":
        raise ValueError("Unsupported named solver version")
    os.environ["SOLVER"]=str(executable)
    with open(os.environ["GITHUB_ENV"],"a") as env:
        env.write("SOLVER="+str(executable)+"\n")
    component_snapshot(line,"before")
    dump(SETUP / "setup-status.json",{"complete":True,"reused_artifact":artifact,
                                    "solver_archive_sha256":solver["sha256"],
                                    "no_build_performed":True})

def measure(line,mode,axioms):
    frozen()
    selected=[r for r in PINS["matrix"] if (r["line"],r["mode"],r["axioms"])==(line,mode,axioms)]
    if len(selected)!=1:raise ValueError("Outside the exact six-pair request")
    before=component_snapshot(line,"immediately-before")
    runner=COLLECTOR / "run.py"
    cmd=["python3",str(runner),"run","--line",line,"--cohort","canonical","--mode",mode,"--axioms",axioms,
         "--inputs",str(ROOT/"inputs"),"--baseline",str(ROOT/"out/baseline/dafny/Dafny"),
         "--final",str(ROOT/"out/final/dafny/Dafny"),
         "--baseline-identity","compilers/baseline/compiler-components.json",
         "--final-identity","compilers/final/compiler-components.json",
         "--baseline-version","compilers/baseline/compiler-version.txt",
         "--final-version","compilers/final/compiler-version.txt",
         "--solver",os.environ["SOLVER"],"--solver-version","setup/solver-version.txt",
         "--output","measurements","--shard","0/1","--jobs","1","--part",PINS["part"]]
    try:
        recorded(cmd,"collector",STATUS/"collector.stdout")
    finally:
        after=component_snapshot(line,"after")
        dump(SETUP/"original-inputs-after.json",modules().inputs(line,ROOT/"inputs"))
        if before["arms"]!=after["arms"] or before["solver_sha256"]!=after["solver_sha256"]:
            raise ValueError("Compiler or solver changed during capture")
    summary=json.loads((ROOT/"measurements/summary.json").read_text())
    complete=(summary["requested"]==summary["completed_pairs"]==1 and summary["incomplete"]==0)
    if len(summary["results"])!=1 or summary["results"][0]["id"]!=PINS["part"]:complete=False
    result=summary["results"][0]
    case=ROOT/"measurements/cases"/str(result["index"]).zfill(4)
    scoped={}
    for side in ("baseline","final"):
        arm=case/side
        actual=json.loads((arm/"result.json").read_text())
        raw=json.loads((arm/"results.json").read_text())
        names=[d["name"] for d in raw["verificationResults"]]
        capture=json.loads((arm/"own-bprint-capture.json").read_text())
        if set(names)!=set(PINS["scope_names"]) or len(names)!=len(PINS["scope_names"]):complete=False
        if actual["timed_out"] or actual["exit"]<0 or not actual["bpl"]["available"] or not capture["captured"]:complete=False
        batches=[b for d in raw["verificationResults"] for b in d.get("vcResults",[])]
        unsupported=[b.get("outcome") for b in batches if b.get("outcome") not in ("Valid","Invalid","OutOfResource")]
        if unsupported:complete=False
        scoped[side]={"exit":actual["exit"],"verdict":actual["verdict"],"warnings":actual["warning_lines"],
                      "declaration_names":names,"batch_outcomes":[b.get("outcome") for b in batches],
                      "resourceCount":sum(d.get("resourceCount",0) for d in raw["verificationResults"]),
                      "unsupported_or_capacity_outcomes":unsupported,"bpl":actual["bpl"]}
    dump(STATUS/"pair-status.json",{"request":selected[0],"diagnostic_complete":complete,
                                  "raw_comparison":result,"arms":scoped,
                                  "boundary":"Diagnostic completion only; Invalid/OutOfResource and every paired difference remain raw. No proof-green or parity waiver."})
    if not complete:raise ValueError("Missing, unsupported or capacity-limited capture")

def report(directory,output):
    found={};problems=[]
    for status in directory.glob("*/run-metadata/pair-status.json"):
        raw=json.loads(status.read_text());tag=raw["request"]["tag"]
        if tag in found:problems.append("Duplicate "+tag)
        found[tag]=raw
    expected={row["tag"] for row in PINS["matrix"]}
    if set(found)!=expected:problems.append("Actual six-pair denominator differs")
    if any(not r["diagnostic_complete"] for r in found.values()):problems.append("Incomplete capture")
    out={"requested_matched_pairs":6,"observed_matched_pairs":len(found),"diagnostic_complete":not problems,
         "missing_or_unsupported_or_capacity":problems,"pairs":found,"producer_run":PINS["producer_run"],
         "compiler_products":PINS["sources"],"boundary":"Six-pair diagnostic, not a trusted proof gate. Capped/unproved controls, errors, warnings and exact differences remain visible."}
    dump(output,out)
    if problems:raise ValueError("Incomplete focused diagnostic; no acceptance")

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("command",choices=("setup","measure","report"))
    parser.add_argument("--line",choices=("dev","shipped"))
    parser.add_argument("--mode",choices=("default","refresh"))
    parser.add_argument("--axioms",choices=("absent","off","on"))
    parser.add_argument("--directory",type=Path)
    parser.add_argument("--output",type=Path)
    args=parser.parse_args()
    try:
        if args.command=="setup":setup(args.line)
        elif args.command=="measure":measure(args.line,args.mode,args.axioms)
        else:report(args.directory,args.output)
    except Exception as error:
        dump(STATUS/"diagnostic-failure.json",{"phase":args.command,"error":str(error),
                                               "diagnostic_complete":False})
        raise

if __name__=="__main__":main()
