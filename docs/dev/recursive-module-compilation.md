# Recursive module compilation (issue 466, stage 7)

The executable targets admitted for recursive module groups are C#, Java, Go, JavaScript, Python and Rust.
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
C++ and resolved Dafny (`dfy`) without needing their
compiler toolchains. Restoring the blanket executable rejection must break the C# positive test; admitting
an unqualified target must break its rejection control. These mutations change only the target
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

Extern linkage preserves two identities: the shared package for generated group
declarations, and each extern module's foreign package and import path. Extern
types keep their specified names; references through their non-null types use
the same identity. Unqualified extern methods in ordinary members complete the
shared package, including methods of generated partial extern classes. Foreign
source files are copied to distinct filenames under the existing experimental
option, preserving generated code and multiple inputs for one package. See
`docs/Compilation/Go.md`, "Recursive module groups", for the linkage convention
and Go's remaining foreign-package restrictions.

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
`git-issue-466-go-extern.dfy` compares extern linkage with C#: module-, type-
and member-level extern names, partial classes, sibling types in foreign method
signatures, qualified generic functions with result coercions, type descriptors and erased
ghost arguments, function values, newtype custom receivers, protected aliases for
foreign dependencies outside the group, built-in type names, multiple foreign
files, calls within and outside a group, complete `.doo` inputs and separately
translated producer/client packages. It repeats the foreign-module comparison
with additional axioms and scheduled resolution.

This is an opt-in compilation feature. It adds no verification condition, axiom,
trusted assumption or verifier-lowering site. With recursive modules off, the
existing Go emission paths are unchanged.

## Python group emission

Python cannot initialize cyclic imports merely by adding the missing sibling
imports: a class definition evaluates its trait bases immediately, before a
partially initialized sibling necessarily has defined them. A group instead
emits one file, named from its first member in ordinal full-name order. All
declarations have a member prefix in that file. References within the group use
those local names, including datatype constructor helpers. A wrapper file for
each member exports the original names, so external clients keep their normal
module-qualified references and share exactly the same class objects.

Each declaration is collected before the file is assembled. Assembly visits
parent traits before a declaration that extends them; datatype constructor
classes follow their datatype base. Importing the file therefore creates the
trait bases before its subclasses, even when each member has a subclass of a
trait in the other member. Constants remain getters. Defaults, witnesses and
type descriptors defer construction until called, after the group has finished
initializing. No new initialization dependency is introduced by their bodies.
Printed datatype, class and iterator names retain their Dafny spelling.

Only dependencies below the group are imported into its implementation file.
Every sibling is registered before the next module is emitted. The optional
replacement-ordering pass visits a whole group together, applies replacements
to its external dependencies, and omits its internal import edges. It retains
the existing rejection of cycles introduced by replacements. A refinement above
the group remains a separate module referring to its original siblings.

The wrappers also preserve separate-compilation interfaces. Their imports use
`--python-module-name` when present. Group and declaration identities and file
assembly order depend on names, so relocating sources and reloading a complete
archive preserve the implementation file.

### Python foreign interfaces

A group member with an extern or omitted declaration is a complete foreign
Python module. No declarations from that member, including ordinary bodies,
are added to the group's implementation, and no generated wrapper replaces its
foreign interface. This follows Python's existing whole-module input convention:
`# Module: Name` overrides the input filename, and otherwise the `.py` filename
selects the module. Dotted extern module names use the ordinary directory
layout. The generated group filename comes from a generated member, so a
foreign name does not become an implementation path. Extern module and declaration names keep their ordinary
spelling; internal group prefixes never appear in the foreign API. Static
functions with a two-argument extern qualifier retain Python's underscore
separator, while static methods retain the dot separator.

A group's foreign interfaces are registered before its implementation file's
imports are emitted, after lower dependencies have been generated. This is necessary even when the foreign member appears
later in compilation order. Generated siblings use module-qualified references
to foreign declarations and datatype constructors. Foreign files must defer
imports of generated siblings until their callables execute: importing a wrapper
and reading its classes during foreign-module initialization can re-enter a
partially initialized implementation. This is a foreign-code initialization
requirement, not an extra verifier assumption.

Ordinary builds copy foreign files supplied with `--input` using the existing
backend convention. For package translation, place those files in the generated
package. Package-mode source compilation imports them in the current package.
A separate client imports the package recorded by the producer. A wholly
omitted module has no record row, so its compiled sibling supplies the group's
package. Package translation of a wholly omitted group remains explicitly
unsupported, as do the existing dotted extern names in package mode. Ordinary
foreign dependencies below a group keep their existing lowering.

`git-issue-466-python.dfy` compares Python with C# for every runtime case of the
Java fixture: source execution, both wrapper-erasure settings, scheduled
resolution, additional axioms and complete `.doo` inputs. Its invalid invariant
construction and reachable false assertion still fail verification.
`git-issue-466-python-link.dfy` adds repeated names in several groups, nested
members, defaults of constrained types, printed classes and iterators, opposing
cross-module trait bases, an entry point inside a group and replacement
ordering. Each entry-point group member depends on a different replacement, so
the compiler must schedule both dependencies before emitting the group.
It compares the implementation emitted from relocated sources byte
for byte, builds those sources into an archive, and runs a separately translated
client against the producer's Python package using the archive and translation
record.
`git-issue-466-python-extern.dfy` now accepts the six former module, type and
member rejection cases. `git-issue-466-python-foreign.dfy` compares complete
foreign-module execution with C#, constructs a foreign datatype, tests renamed
classes and members and explicit static qualifiers, exercises callbacks through
each group, inherits a lower generated trait in two foreign modules, and runs
both a complete archive input and a separately translated
client using the producer's package and translation record. Its negative control
rejects a wholly omitted group in package mode.

This extends an opt-in compilation feature. It changes no verifier rule,
verification condition, axiom, trusted assumption or obligation producer.
With recursive modules off, declaration names, imports and emission stay on
their existing paths.

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

The Python milestone depends on https://github.com/erniecohen/dafny/pull/535,
through it https://github.com/erniecohen/dafny/pull/533, and through that
https://github.com/erniecohen/dafny/pull/529. Python foreign linkage is described
above. C++ and resolved Dafny remain unqualified. This milestone
does not complete issue 466.


## Rust group emission

Rust places each group in one generated container. Member modules remain distinct
inside it, and public facades at the original module paths re-export their
contents. This preserves type identity, associated constructors, constants,
entry-point calls and references from separately translated clients. Rust permits
forward module references, so no runtime initialization ordering is required.
Container names are chosen deterministically from member names and checked for
collisions after Rust identifier escaping. Nested facade modules merge with other
modules at the same enclosing path.

The Rust milestone depends on https://github.com/erniecohen/dafny/pull/535.
It extends only the existing experimental option and changes no verifier rule.
Extern modules, types and members inside a group are explicitly rejected pending
a linkage convention. Rust's existing implicit datatype-default and general-trait
limitations remain: the runtime comparisons use the existing three-member producer
with explicit array initializers and reference traits (`--general-traits=legacy`).
C# comparisons disable wrapper erasure, which Rust already disables. Group programs
that require implicit array initialization are rejected rather than emitted with
placebo elements. Supporting implicit defaults is a separate milestone.

`git-issue-466-rust.dfy` compares the resulting recursion, constants, values,
witness construction, traits, refinement and complete-archive execution with C#.
It also tests repeated declaration names, nested groups, Rust keywords, entry-point
arguments and a separately translated producer/client pair compiled into one crate.
The facades export constructors through their types; clients do not need the
private layout of a group container. `git-issue-466-rust-rejected.dfy` supplies
module, type and member extern controls and an implicit-array-initialization control.
