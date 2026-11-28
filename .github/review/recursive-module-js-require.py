#!/usr/bin/env python3
"""Qualify CommonJS module objects against independent C# group emission."""
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    dll, solver, inputs, directory = (Path(a).resolve() for a in sys.argv[1:])
    directory.mkdir(parents=True, exist_ok=True)
    commands = 0

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
        args += list(map(str, files)) + list(map(str, extra))
        result = subprocess.run(args, cwd=directory, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=120)
        (directory / (label + ".log")).write_text(result.stdout)
        assert result.returncode == expected, f"{label}: exit {result.returncode}\n{result.stdout}"
        assert not any(s in result.stdout.lower() for s in
                       ["out of resource", "timed out", "time out"]), result.stdout
        commands += 1
        return result.stdout

    def write(name, contents):
        path = directory / name
        path.write_text(contents)
        return path

    library = (inputs / "git-issue-466-js-extern-library.dfy").read_text()
    assert library.count('module {:extern "ForeignB"} B {') == 1
    assert library.count('module {:extern} NativeOnly {') == 1
    library = library.replace('module {:extern "ForeignB"} B {',
        'module {:extern "ForeignB", "./issue466_required.js"} B {\n'
        '  function {:extern} NativeCount(n: nat): (r: nat) ensures r == n\n'
        '  function {:extern} NativeEcho(x: A.Tree<int>): (r: A.Tree<int>) ensures r == x')
    library = library.replace('module {:extern} NativeOnly {',
        'module {:extern "NativeOnly", "./issue466_required_zero.js"} NativeOnly {')
    library = library.replace("witness Positive(Identity(2))", "witness Positive(Identity(2) + NativeOnly.Zero())")
    library = library.replace("B.Middle + Identity(1)", "B.Middle + Identity(1) + NativeOnly.Zero()")
    library = library.replace('  class {:extern "Actual"} Native {', '''  class {:extern} RequiredCell<T(0)> {
    constructor {:extern} (ghost erased: int, seed: T)
      ensures Read() == seed
    function {:extern} Read(): T
    function {:extern} InitialValue(): T
  }
  class {:extern "Actual"} Native {''')
    producer = write("producer.dfy", library)
    client = (inputs / "git-issue-466-js-extern-client.dfy").read_text()
    client = client.replace('print counter.Eval(4)', 'print B.NativeCount(5), "\\n";\n    print counter.Eval(4)')
    client = client.replace("B.Echo(", "B.NativeEcho(")
    client = client.replace('    var native := new B.Native();',
        '    var cell := new B.RequiredCell<int>(99, 41);\n'
        '    print cell.Read(), " ", cell.InitialValue(), "\\n";\n'
        '    var native := new B.Native();')
    consumer = write("client.dfy", client)
    source = write("source.dfy", 'include "producer.dfy"\ninclude "client.dfy"\n')
    for name in ["issue466_required.js", "issue466_required_zero.js", "issue466_required_peer.js"]:
        shutil.copyfile(inputs / name, directory / name)
    prelude = inputs / "issue466_required_prelude.js"
    foreign_cs = write("foreign.cs", (inputs / "git-issue-466-js-extern-foreign.cs").read_text() +
        '\nnamespace ForeignB { public class RequiredCell<T> {\n'
        'private readonly T value; private readonly T initial;\n'
        'public RequiredCell(Dafny.TypeDescriptor<T> rtd, T seed) { value = seed; initial = rtd.Default(); }\n'
        'public T Read() { return value; } public T InitialValue() { return initial; } }\n'
        'public partial class __default {\n'
        'public static System.Numerics.BigInteger NativeCount(System.Numerics.BigInteger n) { return Count(n); }\n'
        'public static A._ITree<System.Numerics.BigInteger> NativeEcho(A._ITree<System.Numerics.BigInteger> x) { return x; } } }\n')
    plain = ["--optimize-erasable-datatype-wrapper:false"]
    common = ["--verify-included-files"]
    oracle = run("source-cs", "run", [source], common + plain +
                 ["--target", "cs", "--input", foreign_cs, "--output", directory / "cs"])
    for label, flags in [("plain", plain), ("scheduled", plain + ["--additional-axioms", "--resolution-threads", "3"]),
                         ("erased", [])]:
        js = run("source-js-" + label, "run", [source], common + flags +
                 ["--target", "js", "--input", prelude, "--output", directory / ("js-" + label)])
        if label == "erased":
            cs = run("source-cs-erased", "run", [source], common +
                     ["--target", "cs", "--input", foreign_cs, "--output", directory / "cs-erased"])
        else:
            cs = oracle
        assert js == cs, f"{label}: independent runtime outputs differ"
    help_text = run("help", "verify", [], ["--help-internal"])
    assert "CommonJS libraries loaded with require" in help_text
    assert "JavaScript and Rust groups may not contain extern declarations" not in help_text
    off = run("option-off", "resolve", [source], expected=2, recursive=False)
    assert "cycle" in off.lower()
    keyword = write("keyword.dfy", '''
module {:extern "default", "node:util"} Native {
  import Back
  function {:extern "isDeepStrictEqual"} Same(x: int, y: int): bool
}
module Back { import Native function Identity(n: int): int { n } }
method Main() { print Native.Same(4, 4), "\\n"; }
''')
    escaped = run("keyword", "run", [keyword], ["--target", "js", "--output", directory / "keyword"])
    assert escaped.endswith("true\n"), escaped
    for name, label in [("Foreign.B", "qualified"), ("bad-name", "punctuation"), ("", "empty"),
                        ("class", "keyword"), ("yield", "strict-keyword"), ("require", "loader"),
                        ("module", "node-module"), ("exports", "node-exports"),
                        ("__filename", "node-file"), ("__dirname", "node-directory"),
                        ("BigNumber", "number-runtime"), ("_dafny", "runtime"),
                        ("_System", "system"), ("_module", "default-module")]:
        invalid = write("invalid-" + label + ".dfy", library.replace('"ForeignB",', '"' + name + '",'))
        expected_exit = 2 if name in {"_System", "_module"} else 3
        rejected = run("invalid-" + label, "translate", [invalid], ["--no-verify"], expected_exit)
        if expected_exit == 2:
            assert "both have CompileName" in rejected
        else:
            assert "require-based extern module names to be unqualified ASCII identifiers" in rejected
    partial = write("partial-constructor.dfy", library.replace(
        'class {:extern} RequiredCell<T(0)> {',
        'class {:extern} RequiredCell<T(0)> { function Generated(): int { 0 }'))
    rejected = run("partial-constructor", "translate", [partial], ["--no-verify"], 3)
    assert "partial classes must use a Dafny constructor body" in rejected
    # Refinement owners have their own runtime qualification matrix. Keep name
    # validation here as well: the group filter must include concrete descendants.
    # The broader filter must also validate require names in refining namespaces.
    invalid_refinement = write("invalid-refinement.dfy", library.replace("class Implementation extends A.Counter", "class Implementation") + '\nmodule {:extern "bad-name", "./issue466_required.js"} RequiredRefined refines B { }\n')
    rejected = run("invalid-refinement", "translate", [invalid_refinement], ["--no-verify"], 3)
    assert "require-based extern module names to be unqualified ASCII identifiers" in rejected
    no_certificate = run("certificate", "build", [producer], plain + ["-t:lib", "--output", directory / "rejected.doo"], 3)
    assert "cannot build a library under --modular-termination" in no_certificate
    # Keep the existing archive trust boundary. Arbitrary extern contracts cannot
    # certify a cross-module trait implementation; archive its concrete variant.
    producer.write_text(library.replace("class Implementation extends A.Counter", "class Implementation"))
    consumer.write_text(client.replace("var counter: A.Counter", "var counter"))
    relocated = directory / "relocated"
    relocated.mkdir(exist_ok=True)
    moved = relocated / "producer.dfy"
    shutil.copyfile(producer, moved)
    archive = directory / "group.doo"
    run("archive", "build", [moved], plain + ["-t:lib", "--output", archive])
    archive_cs = run("archive-cs", "run", [consumer], common + plain +
                     ["--target", "cs", "--input", archive, "--input", foreign_cs, "--output", directory / "archive-cs"])
    archive_js = run("archive-js", "run", [consumer], common + plain +
                     ["--target", "js", "--input", archive, "--input", prelude, "--output", directory / "archive-js"])
    assert archive_js == archive_cs, "archive runtimes differ"
    emitted_producer = directory / "producer.js"
    emitted_consumer = directory / "consumer.js"
    run("translate-producer", "translate", [producer], ["--no-verify", "--include-runtime"] + plain +
        ["--output", emitted_producer])
    run("translate-consumer", "translate", [consumer], ["--no-verify", "--include-runtime:false"] + plain +
        ["--library", archive, "--output", emitted_consumer])
    # Each CommonJS export must be bound once, outside the group closure. This
    # also catches a redundant require that Node's cache would otherwise hide.
    emitted = emitted_producer.read_text()
    for module, path, declaration in [("ForeignB", "./issue466_required.js", "let A;"),
                                      ("NativeOnly", "./issue466_required_zero.js", "let NativeClient;")]:
        line = f'let {module} = require("{path}");'
        assert emitted.count(f'require("{path}")') == 1, module
        assert line in emitted, module
        start = emitted.index(declaration)
        assert start < emitted.index(line) < emitted.index("(function()", start), module
    link = subprocess.run(["node", str(inputs / "git-issue-466-js-extern-link.js"), str(prelude),
                           str(emitted_producer), str(emitted_consumer)], cwd=directory, text=True,
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
    (directory / "link.log").write_text(link.stdout)
    assert link.returncode == 0, link.stdout
    linked_cs = run("link-cs", "run", [consumer], ["--no-verify"] + plain +
                    ["--target", "cs", "--input", archive, "--input", foreign_cs, "--output", directory / "link-cs"])
    assert linked_cs == "\nDafny program verifier did not attempt verification\n" + link.stdout
    negative = write("negative.dfy", '''include "producer.dfy"
module Negative {
  import A
  import B
  method Wrong() { assert B.NativeCount(4) == 5; }
  method Live() { assert B.NativeCount(4) == 4; assert false; }
  method WrongConstructor() { var c := new B.RequiredCell<int>(99, 41); assert c.Read() == 42; }
  method LiveConstructor() { var c := new B.RequiredCell<int>(99, 41); assert c.Read() == 41; assert false; }
  method WrongEcho() { assert B.NativeEcho(A.Leaf(4)).value == 5; }
  method LiveEcho() { assert B.NativeEcho(A.Leaf(4)).value == 4; assert false; }
}
''')
    for flags, suffix in [([], "plain"), (["--additional-axioms"], "axioms")]:
        bad = run("negative-" + suffix, "verify", [negative], flags, 4)
        assert bad.count("Error: assertion might not hold") == 6, bad
        assert "6 errors" in bad, bad
    print(oracle, end="")
    print("CommonJS group runtime matrix, archive, separate linkage and 12 negative/vacuity controls passed.")
    print(f"Dafny commands checked: {commands}.")


if __name__ == "__main__":
    main()
