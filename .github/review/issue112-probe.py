import json, pathlib, subprocess, sys
out=pathlib.Path("diagnostics"); out.mkdir(exist_ok=True)
cases={}
for w in [0,1,8,32,64,128]:
 cases[f"bv{w}-literal"]=f"method Test(m: map<bv{w},bool>) {{ assert m[0 := true][0]; }}"
 cases[f"bv{w}-variable"]=f"method Test(m: map<bv{w},bool>, k: bv{w}) {{ assert m[k := true][k]; }}"
cases["nat"]="method Test(m: map<nat,bool>) { assert m[0 := true][0]; }"
cases["bv64-identity"]="method Test(m: map<bv64,bool>) { assert (0 as bv64) == 0; assert m[0 := true][0]; }"
cases["bv64-map-domain"]="method Test(m: map<bv64,bool>) { assert 0 in m[0 := true]; }"
cases["bv64-value"]="method Test(m: map<nat,bv64>) { assert m[0 := 0][0] == 0; }"
rows=[]
for name,source in cases.items():
 for refresh in ["false","true"]:
  for axioms in ["false","true"]:
   dest=out / f"{name}-r{refresh}-a{axioms}"; dest.mkdir()
   src=dest / "case.dfy"; src.write_text(source+"\n")
   cmd=["dotnet","out/dafny/Dafny.dll","verify",str(src),"--solver-path",sys.argv[1],"--type-system-refresh:"+refresh,"--additional-axioms:"+axioms,"--resource-limit","16000000","--verification-time-limit","30","--cores","1","--bprint",str(dest/"input.bpl"),"--solver-log",str(dest/"solver.smt2")]
   r=subprocess.run(cmd,text=True,capture_output=True)
   (dest/"stdout.txt").write_text(r.stdout); (dest/"stderr.txt").write_text(r.stderr)
   rows.append(dict(case=name,refresh=refresh,axioms=axioms,exit=r.returncode,stdout=r.stdout))
(out/"summary.json").write_text(json.dumps(rows,indent=2))
print("\n".join(f"{r['case']} refresh={r['refresh']} axioms={r['axioms']} exit={r['exit']}" for r in rows))
