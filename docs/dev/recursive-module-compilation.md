# Recursive module compilation (issue 466, stage 7)

The executable targets admitted for recursive module groups are C#, Java, Go, JavaScript and Python.
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
Rust, C++ and resolved Dafny (`dfy`) without needing their
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

Qualified foreign functions inherited by a refinement above a group use the same
Go dot selectors as calls within the group. With `--recursive-modules`, non-null
extern class references also retain their renamed class identity outside the
group, including allocation in a refinement. These paths previously used a
Dafny module separator or the source class name in the refining package.

`git-issue-466-go-foreign-matrix.dfy` compares the three-member recursion,
unordered constants, mutually recursive datatypes, implicit defaults, invariant
witnesses, trait dispatch, refinement and complete archive inputs with independent
C# emission. Foreign identity calls occur in the recursion, constants, witness,
datatype construction and trait implementation. A partial extern class is completed
in the shared package and, separately, in the refining package. The source run
also repeats with additional axioms and scheduled resolution. As in the Rust
extern matrix, the archive uses a concrete counter because arbitrary extern
contracts cannot form a modular termination certificate.

The remaining foreign-package boundaries are Go language constraints, not an
ordering restriction that group emission can lift. A foreign package imported by
the generated shared package cannot import that package back: Go rejects the
import cycle. A partial extern class owned by a foreign package cannot receive
generated methods in the shared package. The compiler now diagnoses that ownership
conflict before emission, including an explicitly qualified foreign class in an
ordinary group member. A non-extern group member can instead declare a partial
class completed in the shared package. `git-issue-466-extern-boundaries.dfy` checks
both ownership errors and the native import-cycle error; the runtime matrix is
the positive control for the supported ownership arrangement. The source comparison
also covers disabled datatype-wrapper erasure. No foreign source
is moved between package identities to circumvent Go's rules.

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
archive preserve the implementation file. Extern modules, types and members
inside a Python group, and declarations marked `{:compile false}`, remain
explicitly rejected pending a foreign-linkage convention; ordinary foreign dependencies below a group keep their existing
lowering.

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
`git-issue-466-python-extern.dfy` has module, type and member rejection controls
for both extern and omitted declarations.

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
https://github.com/erniecohen/dafny/pull/529. Python foreign linkage within
groups remains separate work. C++ and resolved Dafny remain
unqualified. This milestone does not complete issue 466.


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
Extern modules, types and members retain the ordinary Rust linkage convention.
Each member keeps its extern attributes and `requiresExterns` flag when moved into
the container. Imports still start at the translation root's `_dafny_externs`,
not at the container; two-argument member externs still name their foreign path
directly. The public facades re-export imported types and default classes as well
as generated declarations. Foreign implementations can therefore name sibling
types and complete generated classes through the same public paths as in an
acyclic program; they never name the group container.

The ordinary Rust diagnostics still reject two-argument module and class externs
and a class that mixes foreign and generated implementations of one trait.
These forms have rejection controls. Abstract refinements of a group member
containing extern types are allowed: they emit no imports or implementations.
The complete extern runtime matrix includes such an abstract refinement of the
member containing a fully imported foreign class.

Concrete refinements may inherit, extend or introduce extern classes with at
most one extern argument and no compiled fields, including fields inherited from
traits. Trait parents follow the ordinary Rust implementation rules. Executable
members may be bodiless foreign members or implemented non-extern functions,
methods and constructors, including functions with method bodies. A refinement
can add or rename foreign members, introduce generic foreign classes, and generate
inherent implementations on those classes. Ghost declarations remain erased.

The refining module imports each class by its compiled name from its own foreign
namespace beneath the translation root's `_dafny_externs`. Ordinary class lowering
then emits inherent implementations on that imported type, including generated
members inherited through an abstract refinement. Each refinement therefore has
its own foreign identity, as in independent C# emission; it does not alias the
ancestor's foreign type or attach generated methods to that ancestor type. Foreign
source must supply each refined type and its foreign members. Generated constructors
use the type's ordinary zero-argument Rust `_allocate_object()` hook, then execute
the Dafny constructor body. Bodiless foreign constructors continue to pass their
arguments directly to the allocation hook. No layout is inferred for the foreign
type. This also works with renamed generic classes, nested namespaces and separate
producer/client translation.

Trait implementations retain their ordinary ownership convention: each parent's
proper methods must be wholly generated or wholly foreign. Foreign source supplies
the trait implementations and upcast support for wholly foreign types and parent
traits not implemented by generated class methods. An empty foreign implementation
can select a trait's generated default methods. These declarations name the public
refining namespaces, without referring to the group container.

Other concrete refinements containing extern types remain rejected, with the type,
refining module and supported class shape named in the diagnostic. Own and inherited
compiled fields still require additional layout rules; ghost fields remain erased.
Foreign members with bodies (including constructors) and two-argument class externs
remain unsupported. A refinement chain through an abstract intermediate module
still checks its concrete descendant. Inherited extern members with explicit
foreign paths remain supported. No new verifier fact or check is involved, and compilation without
`--recursive-modules` is unchanged.

Rust's existing implicit datatype-default and general-trait limitations remain:
the runtime comparisons use the existing three-member producer with explicit
initializers for nonempty arrays and reference traits (`--general-traits=legacy`).
C# comparisons disable wrapper erasure, which Rust already disables.

Empty arrays may omit their element initializer, including when the zero extent
is computed at runtime. Rust's required `--enforce-determinism` already makes
verification prove that an array without an initializer has a zero extent.
For allocations that would otherwise require default elements, the group lowering
evaluates every dimension once, in source order, and checks that at least one is
zero before allocating and finalizing the empty array. The check halts even with
verification skipped if all extents are nonzero. There are no element slots to
initialize, so recursive datatype and invariant-bearing element types need no Rust
`Default` implementation or evaluation of a witness. Generic auto-initializable
parameters retain the backend's existing Rust `Default` bound. Explicit element
initializers retain their existing lowering. Nonempty arrays still require an explicit
initializer under `--enforce-determinism`; this does not add general datatype
default construction or change verification. Programs without a recursive group
retain their existing array emission.

`git-issue-466-rust-empty-arrays.dfy` compares source and complete-archive execution
with independent C# emission for the existing recursion, constants, recursive
datatypes, witnesses, traits and refinement matrix. It adds dynamic empty arrays
of different ranks, a zero in each dimension position, generic elements and
inherited allocation in a refinement. A function-by-method prints the dimension
evaluation order, including dimensions after a zero extent. Controls retain the
verifier's nonempty-array and reachable-false failures, exercise the runtime halt
with verification skipped, and compare emission with the option off.

`git-issue-466-rust.dfy` compares the resulting recursion, constants, values,
witness construction, traits, refinement and complete-archive execution with C#.
It also tests repeated declaration names, nested groups, Rust keywords, entry-point
arguments and a separately translated producer/client pair compiled into one crate.
The facades export constructors through their types; clients do not need the
private layout of a group container. `git-issue-466-rust-rejected.dfy` supplies
two-argument module and class extern controls, a mixed trait-implementation control
and a refinement-with-extern-types control.

`git-issue-466-rust-extern.dfy` compares foreign linkage with C#'s independent
module emission for three-member recursion, constants without a module order,
mutually recursive datatypes, explicit defaults, member-invariant witnesses,
reference traits, refinement and complete library archives. A separately translated
producer/client pair also links through the public facades with the same foreign
source. The archive variant uses a concrete counter: arbitrary extern contracts
cannot receive a modular termination certificate; the source comparison separately
checks the cross-module trait implementation. No certificate or runtime-trust rule
is changed. It covers zero- and one-argument module externs, nested foreign names,
renamed instance members,
two-argument static members, partial foreign classes and foreign implementations
of generated class methods. An extern round trip of a mutually recursive datatype
checks sibling type identity across the foreign boundary. The ordinary Rust extern
tests are option-off controls.

`git-issue-466-rust-imported-refinement.dfy` extends the complete extern runtime
matrix with concrete refinements of fully imported classes. Independent C# and
Rust runs compare three-member recursion, unordered constants, recursive datatypes,
explicit defaults, invariant witnesses, traits, refinement and complete archives.
Distinct foreign return values check the identity of each refining namespace;
a renamed generic foreign class transports a recursive datatype. A separately
translated client exercises the producer's foreign imports through the translation
root. Negative controls retain the rejection of own and inherited fields and
foreign members with generated bodies, including fields added by a refinement.
A reachable false assertion after foreign construction and a call remains rejected. This milestone
adds no verifier fact, axiom or assumption, and changes no emission with
`--recursive-modules` disabled.

`git-issue-466-rust-extended-refinement.dfy` qualifies extensions and newly declared
foreign classes against independent C# emission using the same full runtime matrix,
complete archives and separately translated producer/client pair. It adds a
renamed foreign member and a generic foreign class in a refinement, a ghost
function used by a proof, and another foreign member above an abstract refinement.
Distinct foreign return values check the refining namespaces, and foreign generic
round trips preserve recursive datatype values. Both an incorrect result assertion
and a reachable false assertion after a new foreign call remain rejected.
The eligibility check depends on the kinds of compiled members and the class
layout, rather than whether declarations were copied by refinement. Compilation
with the option disabled and all verifier rules remain unchanged.

`git-issue-466-rust-partial-refinement.dfy` extends the same runtime matrix with
generated functions, methods and function-by-method bodies on foreign classes,
including a renamed generic class transporting values through a foreign member.
It compares independent C# and Rust source runs, complete archives and separately
translated producer/client outputs. A concrete descendant above an abstract
refinement exercises inherited implementations and another generated method;
distinct foreign return values distinguish the refining namespaces. Incorrect
result and reachable false assertions remain rejected after a generated method
calls a foreign member. A constructor with a compiled field retains its layout
rejection, alongside the existing fields and foreign-body controls.

`git-issue-466-rust-constructor-refinement.dfy` qualifies generated constructors
using the same independent C#/Rust runtime matrix, complete archives and separate
producer/client translation. An unnamed constructor calls foreign setters in order;
a named generic constructor transports a recursive datatype. Both are inherited
through an abstract refinement, with distinct foreign namespaces for the concrete
types. Foreign setter traces detect skipped, duplicated or reordered execution.
Incorrect-result assertions for both classes and a reachable false assertion remain
rejected after construction. A foreign constructor with a body has its own explicit
rejection control. Allocation remains supplied by foreign source, and generated
constructors on classes with compiled fields remain rejected. No verifier rule,
fact or assumption changes.

`git-issue-466-rust-trait-refinement.dfy` qualifies generated and wholly foreign
implementations of a generic trait against independent C# emission through the full
runtime matrix, complete archives and separate producer/client linkage. The trait
inherits a default method and an erased ghost field. Concrete descendants through
an abstract refinement use their own trait and foreign type identities. Calls
through generic trait objects and parent upcasts preserve recursive datatype values;
distinct foreign tags check the namespace pairing. Incorrect-result assertions for
both implementation paths and a reachable false assertion remain rejected. Negative
controls reject a compiled field inherited through two traits and the ordinary Rust
mixed-implementation case. This changes no verifier rule or default-option emission.
