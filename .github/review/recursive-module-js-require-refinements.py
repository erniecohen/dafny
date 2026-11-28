#!/usr/bin/env python3
"""Compare CommonJS refinement ownership with independent C# compilation."""
from pathlib import Path
import shutil
import subprocess
import sys


def main(required_ancestors=False):
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

    ancestor = ""
    if required_ancestors:
        ancestor = (inputs / "git-issue-466-js-acyclic-ancestors-ancestor-library.dfy").read_text()
        ancestor = ancestor.replace('module {:extern "ForeignOutside"}',
            'module {:extern "ForeignOutside", "./ForeignOutside.js"}')
        ancestor = ancestor.replace('  trait {:extern "NativeUnused"}', '''  function Generated(n: nat): nat { n }
  function {:extern} Check(n: nat): (r: nat) ensures r == n
  method {:extern} NativeObject(n: nat) returns (value: object)
  trait {:extern "NativeUnused"}''')
        group = (inputs / "git-issue-466-js-acyclic-ancestors-group.dfy").read_text()
        group = group.replace('module {:extern "ForeignB"}',
            'module {:extern "ForeignB", "./ForeignB.js"}')
        library = ancestor + "\n" + group
    else:
        library = (inputs / "git-issue-466-js-trait-refinement-library.dfy").read_text()
    for name, parent in [("ForeignRefined", "B"), ("ForeignFinal", "AbstractForeign")]:
        header = "RefinedB" if parent == "B" else "Final"
        original = f'module {{:extern "{name}"}} {header} refines {parent} {{ }}'
        assert library.count(original) == 1
        library = library.replace(original, f'''module {{:extern "{name}", "./{name}.js"}} {header} refines {parent} {{
  function {{:extern}} RequiredCount(n: nat): (r: nat) ensures r == n
}}''')
    producer = write("producer.dfy", library)
    client = (inputs / ("git-issue-466-js-acyclic-ancestors-client.dfy" if required_ancestors
                        else "git-issue-466-js-trait-refinement-client.dfy")).read_text()
    if required_ancestors:
        client = client.replace("    Client.Main();", '''    Client.Main();
    assert Outside.Check(6) == 6;
    print Outside.Check(6), "\\n";''')
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
    if required_ancestors:
        native = native.replace("let Foreign =", "Foreign =").replace("let NativeOnly =", "NativeOnly =")
        prelude = write("foreign.js", "let Foreign, NativeOnly;\n{\n" + native + "\n" +
            (inputs / "git-issue-466-js-acyclic-ancestors-foreign.js").read_text() + '''
ForeignOutside.__default = class {};
globalThis.issue466RefiningOwners = { ForeignB, ForeignRefined, ForeignFinal };
globalThis.issue466AncestorOwner = ForeignOutside;
}
''')
        write("ForeignOutside.js", '''// Publish before entering a native CommonJS cycle.
const captured = globalThis.issue466AncestorOwner;
module.exports = captured;
const peer = require('./ancestor-peer.js');
captured.Check = n => peer.Check(n);
captured.NativeObject = n => globalThis.issue466RefiningOwners.ForeignFinal.NativeViewer.Make(n);
''')
        write("ancestor-peer.js", '''const captured = require('./ForeignOutside.js');
const root = captured.NativeOutsideRoot;
const genericRoot = captured.NativeOutsideGenericRoot;
module.exports.Check = n => {
  if (require('./ForeignOutside.js') !== captured ||
      root !== captured.NativeOutsideRoot || genericRoot !== captured.NativeOutsideGenericRoot) {
    throw new Error("required ancestor identity changed");
  }
  return captured.__default.Generated(n);
};
''')
    required_owners = ["ForeignRefined", "ForeignFinal"] + (["ForeignB"] if required_ancestors else [])
    for name in required_owners:
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
    if required_ancestors:
        cs_native.write_text(cs_native.read_text() + '''
namespace ForeignOutside { public partial class __default {
public static System.Numerics.BigInteger Check(System.Numerics.BigInteger n) { return Generated(n); }
public static object NativeObject(System.Numerics.BigInteger n) { return ForeignFinal.NativeViewer<System.Numerics.BigInteger>.Make(n); }
} }
''')
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
    if required_ancestors:
        rejected = run("whole-group-certificate", "build", [producer], plain +
                       ["-t:lib", "--output", directory / "rejected-hierarchy.doo"], 3)
        assert "cannot build a library under --modular-termination" in rejected, rejected
    relocated = directory / "relocated"
    relocated.mkdir(exist_ok=True)
    moved = relocated / "renamed.dfy"
    # The acyclic-ancestor hierarchy cannot form a whole-group certificate with
    # arbitrary foreign contracts. Archive its complete ancestor owner, then
    # compile that archive together with the group, retaining this rejection.
    if required_ancestors:
        moved.write_text(ancestor)
        archived_group = write("archive-group.dfy", producer.read_text().removeprefix(ancestor))
        consumer_flags = ["--input", archived_group]
    else:
        shutil.copyfile(producer, moved)
        consumer_flags = []
    archive = directory / "group.doo"
    run("archive", "build", [moved], plain + ["-t:lib", "--output", archive])
    archived = []
    for target, foreign in [("cs", cs_native), ("js", prelude)]:
        archived.append(run("archive-" + target, "run", [consumer], common + plain + consumer_flags +
            ["--target", target, "--input", archive, "--input", foreign,
             "--output", directory / ("archive-" + target)]))
    assert archived[0] == archived[1], "archive runtimes differ"
    emitted_producer = directory / "producer.js"
    emitted_consumer = directory / "consumer.js"
    emission_files = [archived_group, archive] if required_ancestors else [producer]
    run("translate-producer", "translate", emission_files, plain +
        ["--no-verify", "--include-runtime", "--output", emitted_producer])
    # The full group hierarchy cannot form a certificate. A separate client
    # consumes the certified ancestor owner and a native object supplied by the
    # group, checking that producer/client code shares the ancestor identities.
    linked_consumer = write("separate-client.dfy", '''module SeparateChecks {
  import Outside
  method {:main} Main() {
    assert Outside.Check(6) == 6;
    print Outside.Check(6), "\\n";
    var value := Outside.NativeObject(7);
    print value is Outside.OutsideRoot, "\\n";
  }
}
''') if required_ancestors else consumer
    run("translate-consumer", "translate", [linked_consumer], plain +
        ["--no-verify", "--include-runtime:false", "--library", archive,
         "--output", emitted_consumer])
    emitted = emitted_producer.read_text()
    for name in required_owners + (["ForeignOutside"] if required_ancestors else []):
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
    linked_cs = run("link-cs", "run", [linked_consumer], plain + consumer_flags +
        ["--no-verify", "--main-method", "SeparateChecks.Main" if required_ancestors else "TraitChecks.Main",
         "--target", "cs", "--input", archive,
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
    assertion_errors = 8
    if required_ancestors:
        negative.write_text(negative.read_text().replace("module Negative {", '''module Negative {
  import Outside
  method WrongAncestor() { assert Outside.Check(6) == 7; }
  method LiveAncestor() { assert Outside.Check(6) == 6; assert false; }
'''))
        assertion_errors += 2
    for label, flags in [("plain", []), ("axioms", ["--additional-axioms"])]:
        errors = run("negative-" + label, "verify", [negative], flags, 4)
        assert errors.count("Error: assertion might not hold") == assertion_errors, errors
        assert f"{assertion_errors} errors" in errors, errors
    for label, before, after in [
            ("name", '"ForeignFinal",', '"bad-name",'),
            ("constructor", 'class {:extern "NativeCell"} Cell<T(0)> {',
             'class {:extern "NativeCell"} Cell<T(0)> { function Generated(): int { 0 }')]:
        invalid = write("invalid-" + label + ".dfy", library.replace(before, after))
        errors = run("invalid-" + label, "translate", [invalid], ["--no-verify"], 3)
        message = ("require-based extern module names to be unqualified ASCII identifiers" if label == "name"
                   else "partial classes must use a Dafny constructor body")
        assert message in errors, errors
    outside = run("ancestor-owner", "translate", [inputs / "git-issue-466-js-ancestor-owner.dfy"],
                  ["--no-verify"], 3)
    assert "extern types in an extern module" in outside, outside
    if required_ancestors:
        for label, text, message in [
                ("ancestor-name", library.replace('"ForeignOutside",', '"bad-name",'),
                 "require-based extern module names to be unqualified ASCII identifiers"),
                ("generated-ancestor", library.replace('trait {:extern "NativeOutsideRoot"} OutsideRoot extends object {',
                    'trait {:extern "NativeOutsideRoot"} OutsideRoot extends object { function Body(): int { 1 }'),
                 "fully imported extern ancestors")]:
            errors = run(label, "translate", [write(label + ".dfy", text)], ["--no-verify"], 3)
            assert message in errors, errors
        errors = run("library-only-owner", "translate", [archived_group],
                     ["--no-verify", "--library", archive], 3)
        assert "ancestor supplied only as a library" in errors, errors
    print("CommonJS " + ("ancestor" if required_ancestors else "refinement") +
          " source, archive and separate-link runtime comparisons passed.")
    print("Both wrapper modes, scheduled resolution, constructors, partial classes and trait identities checked.")
    print(f"{2 * assertion_errors} negative/vacuity assertions and {8 if required_ancestors else 4} rejection controls passed.")
    print(f"Dafny commands checked: {commands}.")


if __name__ == "__main__":
    main()
