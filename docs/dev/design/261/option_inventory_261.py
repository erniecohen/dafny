#!/usr/bin/env python3
"""Generate a conservative, source-based option audit baseline for issue 261.

Usage: python3 option_inventory_261.py DAFNY_CHECKOUT BOOGIE_CHECKOUT OUT.tsv
This is an inventory aid, not a C# parser and not a proof that reflection has
enumerated every effective option. The implementation milestone must compare a
runtime enumeration with an explicit disposition registry and reject unknowns.
"""
from pathlib import Path
import csv
import re
import sys

dafny, boogie, dest = map(Path, sys.argv[1:])
pins = {"dafny": "428e348b0b125cb67505f03a0026b3f497f98e48",
        "boogie": "45a8869bb43456f126577f5ba6fd24725ed6e4d9"}
rows = set()


def add(repo, path, source, match, kind, name, value_type):
    line = source.count("\n", 0, match.start()) + 1
    policy = "KEY effective value; reject if value cannot be frozen"
    if kind == "legacy-switch":
        policy = "KEY ordered switch tokens plus final effective values; reject unclassified effects"
    if any(s in value_type for s in ("Writer", "Reader", "Action<", "Func<", "Plugin", "Backend", "Printer")):
        policy = "REJECT until typed deterministic adapter and replay contract exist"
    rows.add((repo, kind, name, value_type, policy,
              f"https://github.com/erniecohen/{repo}/blob/{pins[repo]}/{path}#L{line}"))


for repo, root in (("dafny", dafny), ("boogie", boogie)):
    if repo == "dafny":
        files = [p for p in (root / "Source").rglob("*.cs")
                 if not any("Test" in s for s in p.relative_to(root).parts[:-1])]
        for p in files:
            src = p.read_text(encoding="utf-8-sig")
            pattern = r"\b(?:static\s+)?(?:readonly\s+)?Option\s*(?P<generic><[^;\n=]+>)?\s+(?P<name>\w+)\s*="
            for m in re.finditer(pattern, src):
                add(repo, p.relative_to(root).as_posix(), src, m, "modern-option",
                    m["name"], "Option" + (m["generic"] or ""))
    paths = (["Source/DafnyCore/DafnyOptions.cs", "Source/DafnyCore/TestGenerationOptions.cs"]
             if repo == "dafny" else [
                 "Source/ExecutionEngine/CommandLineOptions.cs",
                 "Source/ExecutionEngine/ExecutionEngineOptions.cs",
                 "Source/ExecutionEngine/CommandLineOptionEngine.cs",
                 "Source/Core/CoreOptions.cs", "Source/Core/PrintOptions.cs",
                 "Source/VCGeneration/VCGenOptions.cs", "Source/Houdini/HoudiniOptions.cs",
                 "Source/Concurrency/ConcurrencyOptions.cs", "Source/Provers/SMTLib/SMTLibOptions.cs",
                 "Source/Provers/SMTLib/SMTLibSolverOptions.cs", "Source/Provers/SMTLib/ProverUtil.cs"])
    for path in paths:
        src = (root / path).read_text(encoding="utf-8-sig")
        # Includes interface properties with no explicit visibility modifier.
        is_interface = path.endswith(("CoreOptions.cs", "PrintOptions.cs", "VCGenOptions.cs",
            "HoudiniOptions.cs", "ConcurrencyOptions.cs", "SMTLibOptions.cs", "ExecutionEngineOptions.cs"))
        access = r"(?:(?:public|protected|internal)\s+)?" if is_interface else r"(?:public|protected|internal)\s+"
        pattern = (r"(?m)^\s*" + access +
                   r"(?:(?:new|virtual|override|readonly|static|const)\s+)*"
                   r"(?P<type>[\w.]+(?:<[^;\n=]+>)?(?:\[\])?)\s+"
                   r"(?P<name>\w+)\s*(?:=|;|\{\s*get|=>)")
        for m in re.finditer(pattern, src):
            # Exclude ordinary local assignments captured as 'return value;'.
            if m["type"] in ("return", "throw", "yield", "case", "namespace", "using"):
                continue
            add(repo, path, src, m, "effective-member", m["name"], m["type"])
        if path.endswith(("CommandLineOptions.cs", "DafnyOptions.cs")):
            for m in re.finditer(r'case\s+"(?P<name>[^"\n]+)"\s*:', src):
                add(repo, path, src, m, "legacy-switch", m["name"], "string")

with dest.open("w", newline="", encoding="utf-8") as f:
    w = csv.writer(f, delimiter="\t", lineterminator="\n")
    w.writerow(("repo", "kind", "name", "type", "initial_disposition", "source"))
    w.writerows(sorted(rows))
from collections import Counter
print(f"{len(rows)} audit occurrences")
for k, n in sorted(Counter((r[0], r[1]) for r in rows).items()):
    print(*k, n)
