# Recursive module compilation (issue 466, stage 7)

The executable targets admitted for recursive module groups are C#, Java, Go and JavaScript.
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

Executable admission is in
`SynchronousCliCompilation.CompileDafnyProgram`: the qualified targets join the library backend
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

`git-issue-466-stage7-targets.dfy` checks the explicit rejection for
Python, Rust, C++ and resolved Dafny (`dfy`) without needing their
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

## Go group emission

`GoCodeGenerator.CreateModule` creates one package and source file for a group,
using its first member in ordinal full-name order. Every member writes into that
same file. Before emitting any declaration, it registers every sibling against the
same package import. References inside the group are unqualified references in
that package; clients import the package once (the existing `goimports` pass
removes duplicate imports). Dependencies below the group retain their packages.
Abstract groups contribute no executable declarations.

A member's index in ordinal full-name order prefixes every top-level declaration
name. The same identity is used for classes, traits, datatypes, default classes and their
generated helpers. Thus identically named declarations in different members stay
distinct. Datatype constructor strings and class strings retain their Dafny
names. Refinement above a group retains its original sibling references, rather
than joining the package or rebinding the group. Library sources retained in a
complete `.doo` reconstruct the same partition and name-based identities.
Source-position order depends on file locations. Name-based identities also
let a client use an equivalent library interface rebuilt after source-file relocation.

Constants with initializers remain getters, including interleaved dependencies
that cannot follow a module order. Datatype defaults are methods, following the
group's grounding constructors; type descriptors defer construction. The eager
package variables are the existing companion records and trait identities. Go
orders their initialization by dependencies within the single package, so a
sibling's identity is available before a companion that uses it. No static
initializer needs another package in the same group to have initialized first.

Extern modules, types and members within a Go group are explicitly rejected:
their foreign-package names need a separate linkage convention before they can
be combined with the generated package. This restriction does not affect an
acyclic module or a foreign dependency outside the group.

`git-issue-466-go.dfy` compares Go against C# for every case of the Java fixture,
with and without wrapper erasure, with scheduled resolution and additional
axioms, and through a complete `.doo` input. It retains the invalid invariant
construction and reachable false assertion. Its additional names fixture has
multiple groups, nested members, repeated type names, defaults of constrained types,
cross-module trait dispatch, printed class and iterator names and dependencies
outside the groups. A separately emitted producer and a client using
`--library` link and run against the same group package. Their fixture deliberately
includes files in an order different from file-name order. The client's verified
archive is rebuilt from renamed copies of those sources, to check that relocation
and the archive round trip preserve the package and declaration identities.
`git-issue-466-go-extern.dfy` checks the foreign-linkage restriction.

This is an opt-in compilation feature. It adds no verification condition, axiom,
trusted assumption or verifier-lowering site. With recursive modules off, the
existing Go emission paths are unchanged.

## JavaScript group emission

JavaScript emits one closure and module object per recursive group. Names of
classes, traits, datatypes, iterators and default classes carry a member prefix;
qualified type, constructor, conversion and entry-point references use the same
identity. Both the group name and member order depend on full module names,
so separately translated clients reconstruct them after source relocation.
The generated constructor strings retain their Dafny names.

Declarations initialize only class objects: sibling type descriptors, constants,
trait identities and datatype defaults are evaluated when used, after all module
closures have initialized. No ordering of the members is needed. Abstract groups
emit nothing, and refinement retains its original sibling references.

`git-issue-466-js.dfy` compares the existing group fixtures against C#, including
wrapper-erasure settings, scheduled resolution, additional axioms, complete
archives, repeated names, nested groups, constrained defaults, value-type trait
conversions and a group entry point. It links a separately translated client to
a producer using an archive rebuilt from relocated source files. The invalid
invariant and reachable false assertion remain errors. Module, type and member
extern controls reject unsupported foreign linkage explicitly.

This milestone depends on https://github.com/erniecohen/dafny/pull/535. It adds
no verifier rule, axiom or trusted assumption. Programs without a recursive
group use their existing emission paths.

## Remaining compilation work

Qualify the other backends one at a time. Each needs runtime comparisons for
three-member function recursion, constants whose dependencies follow no module
order, mutually recursive datatypes, defaults and witnesses, traits, refinement
and library inputs. Targets that forbid cyclic imports need group emission as
one target-language unit. No backend is admitted merely because its generated
source parses or compiles.

The Go milestone depends on https://github.com/erniecohen/dafny/pull/533 and,
through it, https://github.com/erniecohen/dafny/pull/529. Python still emits
separate files with imports and class definitions that can need a sibling before
it is initialized. Bypassing admission fails with a missing sibling name; adding
that import fails at a cross-module trait base in a partially initialized module.
Python needs group-level emission and initialization work. Rust, C++
and resolved Dafny remain unqualified too.
