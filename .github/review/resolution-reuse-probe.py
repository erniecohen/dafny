"""Record expected failures of independent filtered CLI invocations; always exit 0."""
import json
from pathlib import Path
import subprocess
import sys


def main():
    dafny, z3, output = sys.argv[1:]
    root = Path(output).resolve()
    root.mkdir(parents=True, exist_ok=True)
    (root / "dfyconfig.toml").write_text('includes = ["*.dfy"]\n')
    (root / "first.dfy").write_text('lemma First() { assert 1 + 1 == 2; }\n')
    (root / "second.dfy").write_text('lemma Second() { assert 2 + 2 == 4; }\n')
    common = [dafny, "verify", "dfyconfig.toml", "--solver-path", z3,
              "--cores", "1", "--resource-limit", "16000000", "--verification-time-limit", "30"]
    records = []
    for name, source in [("valid", 'lemma Second() { assert 2 + 2 == 4; }\n'),
                         ("resolution-error", 'lemma Second() { assert missingName; }\n'),
                         ("parse-error", 'lemma Second() { assert ; }\n')]:
        (root / "second.dfy").write_text(source)
        for selected in ["first.dfy", "second.dfy"]:
            label = name + "-" + selected[:-4]
            command = common + ["--filter-position", selected, "--rprint", label + ".resolved.txt"]
            try:
                result = subprocess.run(command, cwd=root, text=True, capture_output=True, timeout=60)
                (root / (label + ".stdout")).write_text(result.stdout)
                (root / (label + ".stderr")).write_text(result.stderr)
                resolved = root / (label + ".resolved.txt")
                both = resolved.exists() and all(word in resolved.read_text() for word in ["First", "Second"])
                records.append({"case": name, "selected": selected, "command": command,
                                "exit": result.returncode, "resolved_both": both})
            except Exception as error:
                records.append({"case": name, "selected": selected, "error": str(error)})
    reproduced = all(r.get("exit") == 0 and r.get("resolved_both") for r in records[:2]) and all(r.get("exit", 0) != 0 for r in records[2:])
    (root / "results.json").write_text(json.dumps({"reproduced": reproduced, "records": records}, indent=2) + "\n")
    (root / "summary.md").write_text("## Independent filtered invocations\n\n" +
        ("Reproduced: both filtered valid invocations print both resolved files; both invocations reject an error in the unselected file.\n" if reproduced else "Reproduction incomplete; inspect retained diagnostics.\n") +
        "\nExpected parse and resolution failures are recorded and do not fail this probe job.\n")

if __name__ == "__main__":
    main()
