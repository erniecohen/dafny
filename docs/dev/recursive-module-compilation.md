# Recursive module compilation (issue 466, stage 7)

The first executable target admitted for recursive module groups is C#.
The feature remains behind the hidden, experimental `--recursive-modules`
option and its existing prerequisites. Other executable targets are rejected
before code generation, with the target named in the diagnostic. Library
production keeps the complete-group archive rules of stage 6.

## Why the existing C# lowering applies

`CsharpCodeGenerator.CreateModule` emits a namespace, rather than a separately
imported compilation unit. All namespaces are emitted into the same C# source,
so a declaration can refer to another namespace regardless of emission order.
Calls, datatype fields and trait implementations already use qualified names.
Abstract modules are omitted as before; refinement keeps its existing nominal
types and references to the original siblings.

Constants with initializers are emitted as getters (or functions where type
descriptors are needed), so their dependencies are evaluated on use. The
resolver's group call graph already rejects constant cycles. A chain that
alternates between modules therefore needs no module initialization order.
Datatype default construction still follows the grounding constructors found
over the group, and invariant-bearing datatypes use their checked witnesses.
The regression exercises both generic and non-generic defaults, with datatype
wrapper erasure enabled and disabled.

The only change to executable admission is in
`SynchronousCliCompilation.CompileDafnyProgram`: C# joins the library backend
as an allowed target. There is no verifier change, added fact, axiom or trusted
assumption. The base rejected every executable group, so this is an opt-in
feature extension and needs no soundness opt-out. Programs without a group
take the same compilation path as before.

## Controls

`git-issue-466-stage7.dfy` executes a three-member function cycle, interleaved
constants, mutually recursive generic and non-generic datatypes, trait dispatch,
a refinement above the group, invariant witnesses and an all-abstract group.
It repeats execution with scheduled resolution and without datatype-wrapper
erasure, then builds a `.doo` and compiles its retained source with `--input`.
As with ordinary libraries, `--library` alone supplies contracts; separate
compilation also requires the compiled implementation.

A bad invariant construction and a reachable false assertion must both fail
verification before C# execution. The earlier termination and export controls
remain in the issue's regression suite. The original resolver continues to
reject the feature's member-invariant prerequisite.

`git-issue-466-stage7-targets.dfy` checks the explicit rejection for Go, Java,
JavaScript, Python, Rust, C++ and resolved Dafny (`dfy`) without needing their
compiler toolchains. Restoring the blanket executable rejection must break the C# positive test; admitting
Python must break its rejection control. These mutations change only the target
admission site and are built and run separately.

## Remaining compilation work

Qualify the other backends one at a time. Each needs runtime comparisons for
three-member function recursion, constants whose dependencies follow no module
order, mutually recursive datatypes, defaults and witnesses, traits, refinement
and library inputs. Targets that forbid cyclic imports need group emission as
one target-language unit. No backend is admitted merely because its generated
source parses or compiles.
