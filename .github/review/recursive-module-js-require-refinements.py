#!/usr/bin/env python3
"""Compare CommonJS refinement ownership with independent C# compilation."""
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    dll, solver, inputs, directory = (Path(a).resolve() for a in sys.argv[1:])
    directory.mkdir(parents=True, exist_ok=True)
    commands = 0

    def write(name, text):
        path = directory / name
        path.write_text(text)
        return path

    def run(label, command, files, extra=(), expected=0, recursive=True):
        nonlocal commands
        args = ["dotnet", str(dll), command, "--type-system-refresh", "--member-invariants",
                "--standard-libraries:false", "--general-traits=legacy", "--general-newtypes",
                "--allow-external-contracts", "--use-basename-for-filename", "--show-snippets:false",
                "--solver-path", str(solver)]
        if recursive:
            args.append("--recursive-modules")
        if command != "resolve":
            args += ["--cores", "2", "--verification-time-limit", "30", "--resource-limit", "50e6"]
        if command == "translate":
            args.insert(3, "js")
        result = subprocess.run(args + list(map(str, files)) + list(map(str, extra)),
                                cwd=directory, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=120)
        (directory / (label + ".log")).write_text(result.stdout)
        assert result.returncode == expected, f"{label}: exit {result.returncode}\n{result.stdout}"
        assert not any(s in result.stdout.lower() for s in
                       ["out of resource", "timed out", "time out"]), result.stdout
        commands += 1
        return result.stdout

    library = (inputs / "git-issue-466-js-trait-refinement-library.dfy").read_text()
    for name, parent in [("ForeignRefined", "B"), ("ForeignFinal", "AbstractForeign")]:
        header = "RefinedB" if parent == "B" else "Final"
        original = f'module {{:extern "{name}"}} {header} refines {parent} {{ }}'
        assert library.count(original) == 1
        library = library.replace(original, f'''module {{:extern "{name}", "./{name}.js"}} {header} refines {parent} {{
  function {{:extern}} RequiredCount(n: nat): (r: nat) ensures r == n
}}''')
    producer = write("producer.dfy", library)
    client = (inputs / "git-issue-466-js-trait-refinement-client.dfy").read_text()
    client = client.replace("    Client.Main();", '''    Client.Main();
    assert RefinedB.RequiredCount(4) == 4 && Final.RequiredCount(5) == 5;
    print RefinedB.RequiredCount(4), " ", Final.RequiredCount(5), "\\n";''')
    extra_client = (inputs / "git-issue-466-js-partial-refinement-client.dfy").read_text()
    consumer = write("client.dfy", extra_client + "\n" + client)
    source = write("source.dfy", 'include "producer.dfy"\ninclude "client.dfy"\n')

    # Existing native fixtures supply constructors, partial classes and captured
    # trait identities. Keep refining owners private to this block: generated
    # bindings must come from require, rather than finding a preloaded name.
    native = (inputs / "git-issue-466-js-partial-refinement-foreign.js").read_text()
    split = native.index("let ForeignRefined =")
    prelude = write("foreign.js", native[:split] + "\n{\n" + native[split:] + "\n" +
        (inputs / "git-issue-466-js-trait-refinement-foreign.js").read_text() + '''
globalThis.issue466RefiningOwners = { ForeignRefined, ForeignFinal };
}
''')
    for name in ["ForeignRefined", "ForeignFinal"]:
        write(name + ".js", f'''// A real CommonJS loader retains this exact object.
const captured = globalThis.issue466RefiningOwners.{name};
module.exports = captured;
const originalTraits = [captured.NativeView, captured.NativeGenericView];
captured.RequiredCount = n => {{
  if (require(__filename) !== captured ||
      originalTraits[0] !== captured.NativeView || originalTraits[1] !== captured.NativeGenericView) {{
    throw new Error("required refinement identity changed");
  }}
  // A copied exports object would leave this native closure without the
  // generated default class and its inherited recursive function.
  return captured.__default.Count(n);
}};
''')
    cs_native = write("foreign.cs",
        (inputs / "git-issue-466-js-partial-refinement-foreign.cs").read_text() + "\n" +
        (inputs / "git-issue-466-js-trait-refinement-foreign.cs").read_text().replace("using System.Numerics;", "") + "\n" +
        "\n".join(f'''namespace {name} {{ public partial class __default {{
public static System.Numerics.BigInteger RequiredCount(System.Numerics.BigInteger n) {{ return Count(n); }}
}} }}''' for name in ["ForeignRefined", "ForeignFinal"]))
    plain = ["--optimize-erasable-datatype-wrapper:false"]
    common = ["--verify-included-files", "--main-method", "TraitChecks.Main"]
    oracle = run("source-cs", "run", [source], common + plain +
                 ["--target", "cs", "--input", cs_native, "--output", directory / "cs"])
    for label, flags in [("plain", plain),
                         ("scheduled", plain + ["--additional-axioms", "--resolution-threads", "3"]),
                         ("erased", [])]:
        actual = run("source-js-" + label, "run", [source], common + flags +
                     ["--target", "js", "--input", prelude, "--output", directory / ("js-" + label)])
        reference = oracle if label != "erased" else run("source-cs-erased", "run", [source], common +
            ["--target", "cs", "--input", cs_native, "--output", directory / "cs-erased"])
        assert actual == reference, f"{label}: independent runtimes differ"
    off = run("option-off", "resolve", [source], expected=2, recursive=False)
    assert "cycle" in off.lower(), off

    # The unchanged certificate boundary disallows arbitrary foreign contracts.
    rejected = run("certificate", "build", [producer], plain +
                   ["-t:lib", "--output", directory / "rejected.doo"], 3)
    assert "cannot build a library under --modular-termination" in rejected, rejected
    producer.write_text(library.replace("class Implementation extends A.Counter", "class Implementation"))
    consumer.write_text(consumer.read_text().replace("var counter: A.Counter", "var counter"))
    relocated = directory / "relocated"
    relocated.mkdir(exist_ok=True)
    moved = relocated / "renamed.dfy"
    shutil.copyfile(producer, moved)
    archive = directory / "group.doo"
    run("archive", "build", [moved], plain + ["-t:lib", "--output", archive])
    archived = []
    for target, foreign in [("cs", cs_native), ("js", prelude)]:
        archived.append(run("archive-" + target, "run", [consumer], common + plain +
            ["--target", target, "--input", archive, "--input", foreign,
             "--output", directory / ("archive-" + target)]))
    assert archived[0] == archived[1], "archive runtimes differ"
    emitted_producer = directory / "producer.js"
    emitted_consumer = directory / "consumer.js"
    run("translate-producer", "translate", [producer], plain +
        ["--no-verify", "--include-runtime", "--output", emitted_producer])
    run("translate-consumer", "translate", [consumer], plain +
        ["--no-verify", "--include-runtime:false", "--library", archive,
         "--output", emitted_consumer])
    emitted = emitted_producer.read_text()
    for name in ["ForeignRefined", "ForeignFinal"]:
        assert emitted.count(f'require("./{name}.js")') == 1, name
    link = write("link.js", '''const fs = require('fs');
const sources = process.argv.slice(2).map(file => fs.readFileSync(file, 'utf8'));
const program = sources.slice(0, 2).join('\\n') + '\\n{\\n' + sources.slice(2).join('\\n') + '\\n}';
new Function('require', '"use strict";\\n' + program)(require);
''')
    linked = subprocess.run(["node", str(link), str(prelude), str(emitted_producer), str(emitted_consumer)],
                            cwd=directory, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, timeout=120)
    (directory / "link.log").write_text(linked.stdout)
    assert linked.returncode == 0, linked.stdout
    linked_cs = run("link-cs", "run", [consumer], plain +
        ["--no-verify", "--main-method", "TraitChecks.Main", "--target", "cs", "--input", archive,
         "--input", cs_native, "--output", directory / "link-cs"])
    assert linked_cs == "\nDafny program verifier did not attempt verification\n" + linked.stdout

    # Negative and inhabited-false controls exercise native calls and generated
    # fields in both concrete namespaces, rather than an unused foreign contract.
    negative = write("negative.dfy", '''include "producer.dfy"
module Negative {
  import RefinedB
  import Final
  method WrongCount() { assert RefinedB.RequiredCount(4) == 5; }
  method LiveCount() { assert Final.RequiredCount(5) == 5; assert false; }
  method WrongConstructor() { var c := new RefinedB.Cell<int>.Init(99, 41, 43); assert c.Read() == 42; }
  method LiveConstructor() { var c := new Final.Cell<int>.Init(99, 41, 43); assert c.Read() == 41; assert false; }
  method WrongField() { var c := new RefinedB.Partial<int>(5); assert c.Stored == 6; }
  method LiveField() { var c := new Final.Partial<int>(5); assert c.Stored == 5; assert false; }
  method WrongRead() { var c := RefinedB.Viewer<int>.Create(3); assert c.Read() == 4; }
  method LiveRead() { var c := Final.Viewer<int>.Create(7); assert c.Read() == 7; assert false; }
}
''')
    for label, flags in [("plain", []), ("axioms", ["--additional-axioms"])]:
        errors = run("negative-" + label, "verify", [negative], flags, 4)
        assert errors.count("Error: assertion might not hold") == 8, errors
        assert "8 errors" in errors, errors
    for label, before, after in [
            ("name", '"ForeignFinal",', '"bad-name",'),
            ("constructor", 'class {:extern "NativeCell"} Cell<T(0)> {',
             'class {:extern "NativeCell"} Cell<T(0)> { function Generated(): int { 0 }')]:
        invalid = write("invalid-" + label + ".dfy", library.replace(before, after))
        errors = run("invalid-" + label, "translate", [invalid], ["--no-verify"], 3)
        message = ("require-based extern module names to be unqualified ASCII identifiers" if label == "name"
                   else "partial classes must use a Dafny constructor body")
        assert message in errors, errors
    outside = run("ancestor-owner", "translate", [inputs / "git-issue-466-js-ancestor-module.dfy"],
                  ["--no-verify"], 3)
    assert "preloaded extern module objects" in outside, outside
    print("CommonJS refinement source, archive and separate-link runtime comparisons passed.")
    print("Both wrapper modes, scheduled resolution, constructors, partial classes and trait identities checked.")
    print("16 negative/vacuity assertions and 4 rejection controls passed.")
    print(f"Dafny commands checked: {commands}.")


if __name__ == "__main__":
    main()
