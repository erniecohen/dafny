#!/usr/bin/env python3
"""Bounded, recorded feature probes; diagnostic mode never blesses failures."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

CASES = {
  "sequence": "newtype NonEmpty = s: seq<int> | |s| > 0 witness [0]\nmethod Test() { var x := [1] as NonEmpty; assert |x| == 1; }\n",
  "ordinal": "newtype Ord = ORDINAL\nlemma RoundTrip(o: ORDINAL) ensures ((o as Ord) as ORDINAL) == o {}\n",
  "datatype": "datatype L = Nil | Cons(head: int, tail: L)\nnewtype Ne = x: L | x.Cons? witness Cons(0, Nil)\nmethod Test() { var n := Cons(1, Nil) as Ne; var t: L := n.tail; assert t == Nil; }\n",
  "tuple": "newtype Pair = p: (int, int) | true witness (0, 0)\nmethod Test() { var p := (1, 2) as Pair; assert p.0 == 1; }\n",
  "arrow": "newtype F = f: (int -> int) | true witness (x: int) => x\nmethod Test() { var f := ((x: int) => x + 1) as F; assert f(4) == 5; }\n",
  "codata": "codatatype Stream = S(head: int, tail: Stream)\nfunction Zeros(): Stream { S(0, Zeros()) }\nnewtype N = s: Stream | true witness Zeros()\nmethod Test() { var s := Zeros() as N; assert s.head == 0; }\n",
  "generic": "datatype Pair<A,B> = P(a: A, b: B)\nnewtype Flip<X,Y> = p: Pair<Y,X> | true witness *\nlemma Test(p: Pair<int,bool>) ensures ((p as Flip<bool,int>) as Pair<int,bool>) == p {}\n",
  "bad-witness": "datatype D = C(x: int)\nnewtype N = d: D | d.x > 0 witness C(0)\n",
}

def main():
  parser = argparse.ArgumentParser()
  parser.add_argument("dafny")
  parser.add_argument("solver")
  parser.add_argument("output")
  parser.add_argument("--extended", action="store_true")
  args = parser.parse_args()
  out = Path(args.output)
  out.mkdir(parents=True, exist_ok=True)
  versions = {}
  for name, exe in (("dafny", args.dafny), ("solver", args.solver)):
    p = subprocess.run([exe, "--version"], capture_output=True, text=True)
    versions[name] = p.stdout.strip()
  rows = []
  for name, source in CASES.items():
    case = out / name
    case.mkdir(exist_ok=True)
    path = case / "case.dfy"
    path.write_text(source)
    command = [args.dafny, "verify", str(path), "--general-newtypes=true", "--type-system-refresh=true",
               "--solver-path", args.solver, "--cores=1", "--resource-limit=16000000",
               "--verification-time-limit=60", "--log-format", "csv;LogFileName=" + str(case / "resources.csv")]
    if args.extended:
      command.append("--extended-newtype-bases")
    try:
      p = subprocess.run(command, capture_output=True, text=True, timeout=120)
      code, stdout, stderr = p.returncode, p.stdout, p.stderr
    except subprocess.TimeoutExpired as exc:
      code, stdout, stderr = 124, str(exc.stdout), str(exc.stderr)
    (case / "stdout.txt").write_text(stdout)
    (case / "stderr.txt").write_text(stderr)
    row = {"case": name, "source_sha256": hashlib.sha256(source.encode()).hexdigest(),
           "command": command, "exit": code, "stdout": stdout, "stderr": stderr}
    rows.append(row)
    print(name, code, stdout.strip().splitlines()[-1:] or stderr.strip().splitlines()[-1:])
  (out / "results.json").write_text(json.dumps({"versions": versions, "extended": args.extended,
                                              "diagnostic_only": True, "cases": rows}, indent=2))

if __name__ == "__main__":
  main()
