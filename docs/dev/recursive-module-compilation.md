# Recursive module compilation (issue 466, stage 7)

The executable targets admitted for recursive module groups are C# and Java.
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
`SynchronousCliCompilation.CompileDafnyProgram`: C# and Java join the library backend
as allowed targets. There is no verifier change, added fact, axiom or trusted
assumption. Java was still rejected by the C# milestone, so admitting it is an
opt-in feature extension and needs no soundness opt-out. Programs without a group
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

`git-issue-466-stage7-targets.dfy` checks the explicit rejection for Go,
JavaScript, Python, Rust, C++ and resolved Dafny (`dfy`) without needing their
compiler toolchains. Restoring the blanket executable rejection must break the C# positive test; admitting
Python must break its rejection control. These mutations change only the target
admission site and are built and run separately.

## Why the existing Java lowering applies

Java emits each module as a package and each declaration as a qualified Java
type. Package references may be cyclic, and `JavaBackend.CompileTargetProgram`
submits all generated Java source files to one `javac` invocation. It does not
compile a package before its siblings. Traits, datatype fields and refinement
therefore keep their existing qualified references, including references to the
original siblings from a refinement. Abstract modules emit no executable code.

Initialized constants are emitted as getters, as in C#, so a chain of constant
dependencies needs no package initialization order. Non-generic datatypes cache
their default values in static fields; generic defaults are methods. The existing
grounding analysis supplies a finite constructor tree across the group. Type
descriptors defer default construction through lambdas, so obtaining a descriptor
does not recursively initialize its default value. Member-invariant witnesses use
the existing checked defaults. No Java emitter or verifier rule changes here.

`git-issue-466-java.dfy` compares C# and Java execution byte for byte for every
case of the C# fixture, also printing a constructed recursive datatype and
entering the non-generic default chain from its sibling first. It repeats the
comparison with wrapper erasure disabled, scheduled resolution and additional
axioms, then executes the client against a complete `.doo` supplied with
`--input`. The existing invalid construction and reachable false assertion must
both fail before Java compilation. The remaining targets retain explicit
rejection controls. Removing Java admission must break the Java runtime test.

## Remaining compilation work

Qualify the other backends one at a time. Each needs runtime comparisons for
three-member function recursion, constants whose dependencies follow no module
order, mutually recursive datatypes, defaults and witnesses, traits, refinement
and library inputs. Targets that forbid cyclic imports need group emission as
one target-language unit. No backend is admitted merely because its generated
source parses or compiles.

This milestone depends on the C# compilation milestone in
https://github.com/erniecohen/dafny/pull/529. Go still emits separate packages,
which cannot form import cycles. The same runtime fixture fails in Python with
a missing sibling name if admission is bypassed. Adding that missing import to
the generated source instead fails at the cross-module trait base, while its
module is partially initialized. Python emits separate files with imports and
class definitions that can need a sibling before it is initialized. Those
backends need group-level emission and initialization work before qualification;
they are not admitted by this milestone. JavaScript, Rust, C++ and resolved
Dafny remain unqualified too.
