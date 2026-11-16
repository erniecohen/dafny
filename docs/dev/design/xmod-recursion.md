# Recursion across module boundaries for programs with invariants

This is a design note, not an implementation.  It is the design gate for full
mutual recursion between types and members with invariants when the recursion
crosses module boundaries.  Nothing here is built.

It builds on:

- the member-invariants plan for
  [issue 105](https://github.com/erniecohen/dafny/issues/105),
  `docs/dev/issue105-member-invariants-plan.md` in
  https://github.com/erniecohen/dafny/pull/384 (cited below as "the 105 plan");
- the rule in [issue 395](https://github.com/erniecohen/dafny/issues/395)
  that a program may not declare both a codatatype and an invariant;
- the plan for cross-module trait termination,
  [issue 211](https://github.com/erniecohen/dafny/issues/211), in
  https://github.com/erniecohen/dafny/pull/380, and
  [`211-m4b-library-openness.md`](211-m4b-library-openness.md);
- modular termination and well-founded orders,
  [issue 266](https://github.com/erniecohen/dafny/issues/266) and
  [issue 245](https://github.com/erniecohen/dafny/issues/245)
  (`--modular-termination`);
- the cache designs of [issue 241](https://github.com/erniecohen/dafny/issues/241) and
  [issue 261](https://github.com/erniecohen/dafny/issues/261):
  [`241-pipeline.md`](241-pipeline.md), [`241-verification-cache.md`](241-verification-cache.md),
  [`261-content-addressed-caches.md`](261-content-addressed-caches.md).

Code references are to `review/4.11.0` at `1920c844b`.  The small programs
quoted here were run with release `v4.11.0+fcb2042d.review.bcc0c421` and Z3
5.1.0, with `--type-system-refresh`.  That release predates
`--modular-termination`; results under that option are quoted from the 211 plan.

## 1. The design in brief

One principle decides the rest:

> **A module is a namespace, a visibility boundary and a unit of checking.  It
> is not a semantic stratum.**  A set of modules that import each other
> cyclically means exactly what one module with all their declarations would
> mean, with each module's export views kept as limits on what proofs may see.

Today the acyclic module order does semantic work.  It is the outermost
component of Dafny's termination metric, and every per-module analysis assumes
it (section 2).  This design moves that work from the module to the **recursive
module group**: a strongly connected component of the module import graph.  A
module that is in no cycle is a group of one, and behaves exactly as today.

| Question | Decision |
|---|---|
| Unit | The recursive module group: an SCC of the import graph, computed after name binding. |
| Meaning | One least fixed point over the whole group, the same as for the group flattened into one module (section 3.1). |
| Which edges may form a cycle | Plain imports only (`import`, `import opened`, imports of export sets).  Refinement, abstract-import (`:`) and replacement edges, and the edges between a module and its submodules, must still point strictly downward (section 3.2). |
| Abstract modules | May form a group, but then every member is abstract.  A group never mixes the two kinds (section 3.3). |
| Checking order | Groups in topological order.  Inside a group: all signatures first, then declarations, then bodies.  Every analysis that is per module today becomes per group (section 4). |
| Libraries | A group lies inside one compilation unit.  A `.doo` library can contain groups, but no group can contain both library and client modules (section 4.4). |
| Termination | The metric becomes (group, SCC, decreases).  Calls inside a group are checked like calls inside a module.  The 211 rules apply at group boundaries where they apply at module boundaries today (section 5). |
| Inhabitance | Grounding, equality support and auto-initialization are computed per group SCC, as a least fixed point (section 6). |
| Caches | A group is one resolution node.  Its key covers every module in it (section 7). |
| Option | `--recursive-modules`, default off.  It requires `--member-invariants`, and so `--type-system-refresh`.  A program with a group of more than one module counts as a program with invariants for the rule of issue 395: it may not declare a codatatype (section 8). |

Two findings came out of the experiments for this note and matter beyond it:

- **Merging a cyclic program into one module is already the right check.**
  The "Tie" program of issue 211 proves `false` when its parts are in four
  modules.  With the same declarations in one module, Dafny reports
  "cannot prove termination" at both dispatched calls (section 2.3).  A group
  gets that check.
- **Replaceable modules already close cycles that nothing checks.**  A
  replacement can import a client of the module it replaces.  The run-time call
  graph then has a cycle that the verifier never sees, and a three-module program
  proves `false` with no warning and no audit finding (section 2.4).  This is an
  existing soundness hole, independent of this feature, and needs its own issue.

## 2. Where Dafny relies on acyclic modules today

### 2.1 The module graph

`ProgramResolver.ComputeModuleDependencyGraph` builds one graph over module
declarations.  Its edges are:

- refinement and replacement bases (`ProgramResolver.cs:707`);
- a module to its submodules, and an export set to its module (`:729-734`);
- an alias import to the module or export it names (`:812`);
- an abstract import to its target (`:823`).

Every cycle is an error, "module definition contains a cycle"
(`ProgramResolver.cs:504-509`).  The topological order gives each module a
`Height` (`:61-67`), which later orders resolution and the instantiation of
replaceable modules (`:664`).

```dafny
module A { import B  datatype T = Leaf | Node(b: B.U) }
module B { import A  datatype U = U(a: A.T) }
// Error: module definition contains a cycle ...: module A -> import B -> module B -> import A -> module A
```

A refinement edge counts the same way:

```dafny
module Spec { import Impl  type T  function F(t: T): int }
module Impl refines Spec { type T = int  function F(t: T): int { 0 } }
// Error: module definition contains a cycle ...: module Spec -> import Impl -> module Impl -> module Spec
```

### 2.2 Analyses that stop at the module boundary

| Analysis | Where | What it assumes |
|---|---|---|
| Call graph | `ModuleDefinition.CallGraph` (`ModuleDefinition.cs:105`).  An inter-module call goes to `InterModuleCallGraph` instead (`CallGraphBuilder.cs:305-325`). | A call into another module never closes a cycle. |
| Recursion test | `ModuleDefinition.InSameSCC` requires both callables to be in the same module (`ModuleDefinition.cs:293-297`). | — |
| Termination checks | Only for calls `InSameSCC` reports as recursive: methods (`Statements/BoogieGenerator.TrCall.cs:124-135`), functions (`BoogieGenerator.ExpressionWellformed.cs:899-902`). | A cross-module call is lower in the metric, because its module is lower. |
| Overrides | An edge from the overridden member to the override, in the override's module (`CallGraphBuilder.cs:64-66`, `:120-136`).  A cross-module call to a trait function also gets an edge in the caller's graph (`:313-315`). | Inside one module, dynamic dispatch is visible to the SCC computation. |
| Cross-module `extends` | Rejected unless the trait has `{:termination false}` or the extender has `@AssumeCrossModuleTermination` (`ModuleResolver.cs:2395-2420`). | This is the one existing escape from the module order: issue 211. |
| Datatype dependencies | `AddDatatypeDependencyEdge` adds an edge only between datatypes of the same module (`NameResolutionAndTypeInference.cs:2911-2924`). | — |
| Grounding and equality support | Per SCC of that graph (`ModuleResolver.cs:1542-1546`, `SccStratosphereCheck` at `:3019`).  A type from another module is summarized by which of its type parameters its grounding constructor uses (`:3082-3084`, used at `:3161-3164`). | The summary of a lower module is final. |
| Codatatype groups | Same-module edges only (`ModuleResolver.cs:1550-1568`). | — |
| Co-recursive calls | `InSameSCC` again (`CoCallResolution.cs:159`). | — |
| Constraint, witness and constant cycles | Reported through the call graph: "recursive constraint dependency" (`ModuleResolver.cs:1961-1969`), "const definition contains a cycle" (`:1681`). | — |

So Dafny's termination metric is really (module, SCC, decreases), as the 211
plan says.  The module component is never written because, with no cycles, an
import can never lead back.

Grounding already works through generic types from other modules, because the
summary is per type parameter:

```dafny
module A { datatype Box<X> = Box(x: X) }
module B {
  import A
  datatype T = T(b: A.Box<T>)      // Warning: ... no instances of datatype 'T' can be constructed
  method M() returns (t: T) { }    // Error: out-parameter 't' ... might be uninitialized
}
```

The same declarations in one module give the same warning.  The rank order on
datatype values is global (it lives in the prelude), so termination by
structure also works across modules today:

```dafny
module A { datatype List<X> = Nil | Cons(head: X, tail: List<X>) }
module B {
  import A
  datatype Tree = Node(kids: A.List<Tree>)
  function Size(t: Tree): nat { 1 + SizeAll(t.kids) }
  function SizeAll(l: A.List<Tree>): nat { if l.Nil? then 0 else Size(l.head) + SizeAll(l.tail) }
}
// 3 verified, 0 errors
```

This is already a recursion through a generic type of another module.  It
works because `A` does not depend on `B`.  The feature is needed when it does.

### 2.3 The same program, one module or four

The Tie program of the 211 plan, as it stands there, in five modules
(`Tr`, `X`, `L`, `Y`, `M`), verifies with `assert false` (8 verified, 0 errors).
The same classes in one module:

```dafny
module One {
  trait T { function A(other: T): int  function B(other: T): int }
  class X extends T { constructor () { }
    function A(other: T): int { 0 }
    function B(other: T): int { other.A(this) } }   // Error: cannot prove termination
  class Y extends T { constructor () { }
    function A(other: T): int { other.B(this) + 1 } // Error: cannot prove termination
    function B(other: T): int { 1 } }
}
```

Inside one module the override edges put `T.A`, `T.B` and the four overrides in
one SCC, and both dispatched calls are checked.  That is the check a group
should get, and it is the evidence for treating a group as one module.

### 2.4 An existing hole: replacement closes cycles

A replacement edge (`module R replaces F`) points from `R` to `F`, like a
refinement.  A module `Bar` that imports `F` is verified against `F`.  Nothing
stops `R` from importing `Bar`:

```dafny
replaceable module Foo { lemma L() ensures false }
module Bar { import Foo  lemma K() ensures false { Foo.L(); } }
module FooImpl replaces Foo { import Bar  lemma L() ensures false { Bar.K(); } }
module Main { import Bar  method Main() { Bar.K(); assert 1 == 2; } }
// 3 verified, 0 errors.  'dafny audit': 0 findings.
```

The module graph is acyclic: `Bar -> Foo`, `FooImpl -> Foo`, `FooImpl -> Bar`.
After replacement the call graph has the cycle `Bar.K -> FooImpl.L -> Bar.K`,
and neither call is checked, because each crosses a module boundary.  The same
shape with functions compiles and overflows the stack at run time.  A bodiless
lemma in a replaceable module is not reported as an assumption, because it is
meant to be supplied by the replacement.

This needs its own issue and a default-on fix.  The simplest fix rejects a
replacement whose import closure reaches a module that imports the replaced
module.  This design depends on it: groups are computed on the graph after
replacement (section 3.2).

## 3. Meaning (question 1)

### 3.1 One least fixed point over the group

Read a program as the issue 395 comment does: a set of typing and judgement
rules, with truth the least predicate closed under them.  Modules partition the
rules by name.  They do not order them.  Under that reading a cycle of modules
is unremarkable, and its meaning is the least fixed point of the rules of the
whole group.

For types with invariants, the 105 plan defines each type as
`T = { v ∈ raw T | Inv_T(v) }`, where the raw type takes its recursive
components from the finished types.  For a group with types
`T₁ … Tₙ` spread over its modules, this is one system of equations

```
Tᵢ = { v ∈ Fᵢ(T₁, …, Tₙ) | Invᵢ(v) }        (i = 1 … n)
```

where `Fᵢ` is the one-step unfolding of `Tᵢ`'s declaration, with the types of
lower groups as constants.  Its meaning is the least solution.  It exists when
each right-hand side is monotone in `T₁ … Tₙ`:

- `Fᵢ` is monotone because the variance check already rejects recursion in
  negative positions.  It does so per declaration, so it does not care which
  module the recursion goes through.
- `Invᵢ` must not refer to the extension of any type that depends on `Tᵢ`.
  That is the extension condition of the 105 plan (its section 3.3).  Across a
  group, "depends on" is computed over the group: if `A.T` has a component of
  type `B.U` and `B.U` one of type `A.T`, each is dependent on the other, and
  the cone of `A.T`'s invariant may not quantify over `B.U` without a
  collection bound.  This is the 105 condition on the flattened group, not a
  new one.

The 105 plan's model construction (its section 5.2) orders types "so that each
comes after the types whose extensions its cone refers to" and builds values by
structural stages.  Neither step mentions modules.  It applies to the flattened
group unchanged, and so its consistency argument does too.

**Why one fixed point, and not one per module.**  A cycle has no
stratification.  A per-module meaning would have to take the other modules'
types as parameters, and closing the loop is the joint fixed point anyway.
Bekič's lemma says the two agree: the least solution of a system can be
computed one variable at a time, with the others' least solutions substituted.
That is what makes checking module by module inside a group sound, as long as
each check is about the joint solution and assumes nothing about it beyond
what the equations give.

**Export views.**  A module of a group sees the other modules through their
export sets, as today.  A type that is only provided is opaque to the importer:
its components and its invariant are hidden (the 105 plan's rule: the invariant
is exported exactly when the type is revealed).  Hiding removes facts from a
proof's context.  It does not change the meaning of the group, so it can only
cost completeness.

**Functions, lemmas and predicates** keep their meaning: a function is the
unique solution of its equation, by well-founded recursion on the group's
termination metric (section 5).  Extreme predicates stay as they are in a
program with invariants: Knaster–Tarski fixed points, per the issue 395
comment.

### 3.2 Which edges may form a cycle

Only plain imports.  Every other edge of the module graph presupposes that its
target is finished, so it must point to a strictly lower group.

- **Refinement.**  The 105 plan (its section 4.2) and Dafny's refinement rely
  on one fact: a refining module inherits its base's proofs without re-proving
  them (`IsInherited` and `MustReverify`), because the base was proved for every
  possible refinement.  If the base is in the refining module's group, the
  base's proofs can depend, through the cycle, on the refining module's own
  declarations.  Example: `A.L` calls `C.M`, `C.M` calls `B.L`, and
  `B refines A`.  `B.L` is inherited and assumed on the strength of `A.L`, while
  `A.L`'s proof used `B.L`.  The inherited body's termination checks would be
  skipped too.  There is a practical reason as well: the refinement transformer
  clones the base before the refining module is resolved, and the base's
  signature would need the refining module's.  So: **a refinement base must be
  in a strictly lower group.**  A module inside a group may refine a module
  below it.
- **Abstract imports.**  `import X : I` means "for an arbitrary refinement of
  `I`".  That presupposes a finished `I`, and the resolver builds `X`'s
  signature by copying `I`'s.  So: **the target of an abstract import must be in
  a strictly lower group.**
- **Replacement.**  A replacement edge is a refinement edge for the purpose of
  verification, and at run time it redirects every edge into the replaced
  module.  Groups are computed on the graph with those edges added.  A
  replacement that would put itself in a group with a client of the module it
  replaces is rejected: the clients were verified against the replaced
  module's contract, which carries no termination bound.  A later design could
  allow it with 211-style global bounds on replaceable members.
- **Submodules.**  A parent depends on its submodules.  A submodule that imports
  a module that imports its parent closes a cycle through that edge.  Lexical
  scoping inside nested modules interacts with the cycle in ways this note
  does not settle, so the first version keeps such cycles an error.

`import opened` adds no difficulty.  Opened names are not re-exported, so name
lookup does not follow the cycle.  If `A` opens `B` and `B` opens `A`, each sees
the other's own declarations and no more, and the existing ambiguity rules
apply.

### 3.3 Abstract modules in a group

A compiled module may not import an abstract one ("a compiled module (Impl) is
not allowed to import an abstract module (User)", `ModuleResolver.cs:1205`).
In a group every module reaches every other, so if one member is abstract,
every member is.  **A group is all concrete or all abstract.**

An all-abstract group is sound.  Its abstract types are uninterpreted
parameters of the fixed point, its abstract imports point below it (section
3.2), and an invariant on an abstract type is a constraint on that parameter.
For each interpretation of the parameters, the group means the least fixed
point of section 3.1.

Its use is limited, and the limit is not new.  Refinement makes fresh types.
Refining one member of a group gives a module whose recursive references still
point at the original group, because it does today for an abstract import:

```dafny
module I { type T }
abstract module User { import X : I  datatype U = U(t: X.T) }
abstract module Impl refines I { import User  datatype T = Leaf | Wrap(u: User.U) }
abstract module UserImpl refines User { import X = Impl }
abstract module Client {
  import Impl  import User  import UserImpl
  method M(a: UserImpl.U, b: User.U) {
    var t: Impl.T := a.t;      // fine
    var w := Impl.Wrap(b);     // fine
    var bad := Impl.Wrap(a);   // Error: incorrect argument type ... (expected U, found U)
  }
}
```

So refining a recursive group into a concrete one would need **group
refinement**: one declaration that refines every member at once and re-ties
the internal references.  Its meaning is clear (the least fixed point of the
refined system), but it needs syntax and a resolver design of its own.  It is
deferred.  An abstract import from a group to a lower interface, instantiated
by refining the whole group, is the same case.

## 4. Checking order (question 2)

### 4.1 The minimal change

1. Build the module graph as today, then add the replacement edges of section
   3.2.
2. Take its SCCs.  A non-trivial SCC is a group if every edge inside it is a
   plain import.  Otherwise report the cycle as today, with a message that names
   the edge that may not be part of a cycle.  With the option off, every
   non-trivial SCC is reported exactly as today.
3. Give each module a `Group` and give each group a height.  Every analysis of
   section 2.2 compares groups where it compares modules today.  A module in no
   cycle is a group of one, so with the option off every comparison gives the
   answer it gives now.
4. Resolve groups in topological order.  Inside a group, run the existing
   phases of `ModuleResolver` across all members before the next phase starts:
   - register every member's top-level declarations and member signatures;
   - compute each member's export sets and the import views of the group's
     other members;
   - resolve declarations: types, parents, constructor signatures;
   - run the per-group analyses: the call graph and its SCCs, datatype
     dependencies, grounding, equality support, auto-initialization, constraint
     and constant cycles, and, with invariants, the 105 cone analysis, the
     extension condition and raw-member inference;
   - resolve bodies, which needs only signatures and can stay per declaration.

   The first and third steps are the real work.  Today `ModuleResolver` runs
   them for one module and publishes a signature that importers then read.  A
   group needs the signatures of all its members before any member's types
   are resolved.

### 4.2 Verification

Verification stays per module and per declaration.  The translator already
works one module at a time, with a visibility scope made from the module's own
declarations and the signatures it imports (`BoogieGenerator.cs:131-145`).
Inside a group the imported signatures include modules that import this one
back.  The Boogie program is a set of declarations, so the order does not
matter.  What a module's translation needs from the group is the result of the
group analyses: which calls are recursive, the SCC representatives, and which
members are raw.  These are fixed before any member is translated.

Each obligation is still proved once, in its own module.  Separate
verification of the members of a group is the same as separate verification
of the declarations of one module today.

### 4.3 Compilation

Compilation needs a check per back end.  Some targets map a Dafny module to a
unit that may not import another cyclically; Go packages are the clearest
case.  The compilation milestone must test each back end, and reject a group
for a target that cannot express it, with a clear error, until that target
emits the group as one unit.  Verification does not wait for this.

### 4.4 Libraries

A `.doo` library is a program that was resolved and verified without its
clients.  None of its modules can name a client module.  So **no group contains
both library and client modules.**  This is a property of what a library is,
not a restriction this design adds.

A library may contain groups.  Its clients resolve the library's source again,
so the client needs the same option, and `--recursive-modules` must be one of
the options a `.doo` records and checks (`DooFile.CheckAndGetLibraryOptions`).

Recursion that must reach from a library into its future clients goes through
the mechanisms built for that, which are modular: an extensible trait with
global bounds under the 211 rules, or an abstract import.

## 5. Termination and well-foundedness (question 3)

### 5.1 The metric

The metric becomes (group, SCC, decreases).  Concretely:

- One call graph per group.  `InSameSCC` compares groups.  Every call whose
  caller and callee are in one SCC of the group's call graph is checked against
  the callee's `decreases` clause, wherever their modules are.
- A call from a group to a lower group is not checked, as a call to a lower
  module is not checked today.
- Override edges are already added in the override's module graph.  Inside a
  group that graph is the group's graph, so dispatch through a trait of the group
  to an override in another member is checked like dispatch inside one module
  (section 2.3).

**Soundness.**  Take an infinite chain of nested calls.  Group heights cannot
increase along it, since every call goes to the same group or a lower one.  So
from some point on it stays in one group.  Inside the group it stays, from some
point on, in one SCC of the group's call graph, since the SCCs are ordered.
Every call in that tail is checked to decrease in a well-founded order, so the
tail is finite.  This is the existing argument with "module" replaced by
"group".  It needs one property: the group call graph has every edge that can
occur at run time between members of the group.  The edges Dafny adds today are
calls, override edges and the type-dependency edges of
`AddTypeDependencyEdges` (`CallGraphBuilder.cs:72-82`), which are restricted to
one module; their test becomes one group.

**Decreases clauses across modules.**  A recursive call into another module
compares the callee's `decreases` expression with the caller's.  Today that
never happens across modules.  The callee's `decreases` clause is part of its
specification, and the check is translated with the callee's expression, as
for a call inside a module.  If the expression uses a function whose body the
caller's module cannot see, proving the decrease may need facts the caller
does not have.  That costs completeness, not soundness.

### 5.2 Invariants that call functions of other modules

- An invariant is not callable (the 105 plan, section 4.1), so it adds no call
  edge.  The functions in its cone are ordinary functions, and their termination
  is checked by the group's call graph.
- The cone of an invariant crosses module boundaries.  Inside a group the
  resolver sees every body, so the cone analysis is the 105 analysis run on the
  group.  Below the group, the cone needs the per-function summaries the 105
  plan already needs for clause 2 (does a function quantify without a bound over
  a type parameter), recorded for library functions too.  A lower module cannot
  name a type of the group, so clauses 1 and 5 cannot be violated there except
  through type arguments, which clause 2 covers, and through dynamic dispatch,
  which clause 4 covers.
- Raw members are members of the type, so they are in the type's module.  A
  function in another module of the group that is applied to a raw value takes
  a parameter of type `raw T`, and is checked without the invariant, as in the
  105 plan.
- The rank order is global (section 2.2), and a value of `T` and of `raw T` have
  the same rank.  Decreasing by structure works across modules as it does today.

### 5.3 Relation to the 211 plan

The 211 plan orders dispatch across module boundaries with global bounds.  This
design changes only what "module" means in it:

| 211 rule | In a program with groups |
|---|---|
| 1. Openness is declared by the trait's module | By the trait's declaration, as before.  "Foreign" extension means extension from another group.  A trait may be extended anywhere in its own group without `{:termination false}`, and the override edges make those calls ordinary recursive calls. |
| 2. Dispatched call through an extensible trait: `b < B` | Unchanged, for implementations outside the group. |
| 3. Known call: `b <= B` plus Dafny's existing check | The existing check is (group, SCC, decreases). |
| 4–7. Overrides, closed bounds, callbacks, orders | Unchanged. |
| 8. Libraries carry a termination record | A group is one producer realization.  Its record covers all members. |

`OpenTraits` and the reach analysis in `ModularTermination.cs`, which compare
modules to decide whether an implementation is foreign, compare groups instead.
M4b section 5.6 rejects cyclic concrete bindings of abstract imports; section
3.2 above keeps abstract imports pointing downward, which agrees.

Without `--modular-termination`, today's escape (`{:termination false}` on a
trait extended from another module) is still available for traits extended
from outside the group, and is still warned about by default.  Inside a group
it is not needed, and a program that moves its cross-module implementations
into the trait's group gets them checked.

## 6. Inhabitance and groundedness (question 4)

- **The datatype dependency graph is per group.**  `AddDatatypeDependencyEdge`
  adds an edge when both datatypes are in the same group.  Grounding and
  equality support then run per SCC of that graph, across modules.  For types
  of lower groups the existing per-type-parameter summary is used, as now.
- **Inhabitance is a least fixed point.**  A type is nonempty if some grounding
  constructor has nonempty arguments.  Over a group SCC this must be computed
  jointly, starting from "possibly empty" for every member.  A per-module
  computation that assumed the other members nonempty would compute a greatest
  fixed point, and would call `A.T = T(b: B.U)` and `B.U = U(a: A.T)` nonempty
  when neither has a value.  That is the vacuity hazard of the 105 plan's
  section 5.3, through a module boundary.
- **Witnesses.**  Under the 105 plan a value type with an invariant is possibly
  empty unless it declares a witness.  A witness is a closed expression, proved
  by the ordinary conversion check.  A witness, a constant initializer and a
  subset-type constraint are nodes of the call graph, so a witness of `A.T` that
  uses a constant of `B` whose initializer uses `A.T`'s witness is a cycle in the
  group call graph, reported as it is inside one module today:

  ```dafny
  module M { const a: int := b + 1  const b: int := a + 1 }
  // Error: const definition contains a cycle: a -> b -> a
  ```

- **Grounding with invariants.**  The 105 plan treats a component of type `D`
  as possibly empty in the grounding of `raw D` unless `D` has a witness.  In a
  group the same holds for a component of any type of the group that has an
  invariant and no witness.
- **Auto-initialization** (`GetAutoInit`, the `(0)` and `(00)` characteristics) is
  computed per group SCC by the same least-fixed-point rule.

## 7. Incremental and cached verification (question 5)

**Resolution keys.**  The local resolution key `L(m)` of the 261 design is
defined over the acyclic module graph: it hashes `L(target)` for each
prerequisite.  A cycle would make that definition circular.  With groups:

```
L(S) = H(frame("dafny/group-resolution-input/v1",
         producer identity, options,
         [owned input of m, for m in S in canonical order],
         sorted [(edge, L(target group))  for edges from S to lower groups]))
L(m) = H(frame(..., L(S), identity of m))           for m in S
```

A group of one gives the existing `L(m)` up to framing; the framing version
changes only under the option.  Every edit to a member of `S` invalidates the
resolution of all of `S`, and everything above it, as an edit to one module
does today.

**The global projection.**  The finalized key `R(m)` of the 261 design adds
`G(m)`, the answers to whole-program analyses that translation reads.  The group
analyses (SCCs, cone results, raw members, grounding) depend only on `S` and
lower groups, so `L(S)` covers them.  Whole-program answers that already go
into `G(m)`, such as the modular-termination classification, stay there, keyed
by group where they are keyed by module now.

**Translation.**  Dependency-local translation
(https://github.com/erniecohen/dafny/pull/285) gives each module's Boogie the
declarations of its dependency closure.  For a member of a group that closure
contains the whole group.  So each member's translation includes every
declaration of the group, and its key depends on `L(S)`.

**Verification results.**  The result cache of `241-verification-cache.md` keys
on the exact prepared task.  It needs no change and keeps its soundness
argument, which is an equality of task inputs.  Early cutoff still works:
editing a method body in `A` retranslates every member of the group, but a task
whose Boogie text is unchanged is a hit.

**Parallel resolution.**  `--resolution-threads` schedules modules on the
import DAG (`241-pipeline.md`, section 6.5).  A group is one node of that DAG.
Its signature phase is sequential.  Its body phase is per declaration and can be
parallel later.

**The trade.**  A group is the unit of invalidation.  A large group costs
incremental precision the way a large module does.  `--show-hints` should list
each group with its members, so users can see what they have joined.

## 8. Backward compatibility (question 6)

- **Opt-in.**  `--recursive-modules`, default off, usable in `dfyconfig.toml`,
  recorded in `.doo` files.  With it off, every cycle is reported with today's
  message, and every module is a group of one.
- **Byte-identical when off.**  The implementation replaces module comparisons
  with group comparisons.  With the option off a group is exactly a module, so
  the code computes the same graphs, the same SCCs and the same Boogie.  Each
  milestone shows unchanged verdicts and resource counts on the suite and the
  standard library with the option off, as `AGENTS.md` requires.
- **Byte-identical without cycles.**  With the option on, a program with no
  cycle has only groups of one, and gets the same output as with it off.  A
  milestone test pins this on the suite.
- **Requires `--member-invariants`.**  Cross-module recursion is part of the
  invariant language.  The issue 395 rule extends to it: a program that has a
  group of more than one module may not declare a codatatype.  Without that,
  co-recursive call detection (`CoCallResolution.cs:159`) and the codatatype
  dependency graph (`ModuleResolver.cs:1550-1568`) would have to be regrouped
  too, and the program would mix least and greatest fixed points across modules,
  which is what issue 395 rules out.  A group without invariants is allowed: it
  is a program in the invariant language that happens to declare none.
- **Mainline programs** have no cycles, because mainline rejects them, so
  nothing they contain changes meaning under any option.

## 9. Staged implementation plan (question 7)

Every milestone is one pull request behind `--recursive-modules`, except M0.
Every milestone shows option-off equality of verdicts and resource counts on
the suite and the standard library, and has a `assert false` control wherever
a positive test relies on a new rule.  Proof cost is compared in resource
units.

| Milestone | Content | Tests |
|---|---|---|
| **M0** (separate issue, default-on soundness fix) | Reject a replacement whose imports reach a client of the replaced module (section 2.4), with a hidden `--legacy-*` opt-out. | The program of section 2.4, rejected; its function form, rejected; a replacement that imports an unrelated module, accepted. |
| **M1**: groups, reported | The option, its dependency on `--member-invariants`, the `.doo` record.  Compute groups after replacement edges.  Cycles through refinement, abstract-import, replacement or submodule edges are errors that name the edge.  Groups are listed by `--show-hints`.  A group of more than one module is still rejected: "not yet supported". | Option off: the cycle messages of section 2.1 unchanged.  A two-module cycle and a three-module cycle (`A -> B -> C -> A`) reported as one group.  A diamond (`D` imports `B` and `C`, both import `A`) is four groups of one.  A cycle through `refines`, through `import X : I`, through a submodule: each an error naming that edge.  A cycle together with a codatatype: rejected by the issue 395 rule. |
| **M2**: groups without invariants | Group resolution in phases (section 4.1).  Per-group call graph, `InSameSCC`, datatype dependencies, grounding, equality support, auto-initialization, constraint and constant cycles. | Mutually recursive datatypes across two modules, with functions over them that verify.  A three-module function cycle with a wrong `decreases` in the middle module: the error is at that call.  A cross-module cycle of datatypes with no base case: the warning of section 2.2 and the definite-assignment error.  A constant cycle across modules: error.  **Cycle through generics**: `A.List<X>`, `B.Tree = Node(A.List<B.Tree>)`, and `A.Forest = Forest(B.Tree)`, so that `A` and `B` form a group through a type argument.  **Diamond with a cycle**: `B` and `C` import each other and both import `A`, and `D` imports both.  Provided-only types across a group: a proof that needs the hidden definition fails, and one that does not, verifies.  Opened imports in both directions, with a name clash that is an ambiguity error. |
| **M3**: traits in a group | Cross-module `extends` inside a group without `{:termination false}`.  `ModularTermination` compares groups. | The Tie program with `Tr`, `X`, `Y`, `L` in one group: "cannot prove termination" at both dispatched calls, as in section 2.3.  The same with a bound that decreases: verifies.  Tie with `X` outside the group: the 211 rules apply, with and without `--modular-termination`. |
| **M4**: invariants across modules | Depends on stages 1 to 4 of the 105 plan.  The cone analysis, the extension condition and raw-member inference over a group, and the per-function summaries for lower groups and libraries. | Datatype invariants whose types are mutually recursive across two and three modules, each invariant calling a function of another module.  The `Liar` program of the 105 plan split over two modules: rejected.  An invariant whose cone quantifies over a type of another module of the group that depends on it: rejected; over one that does not: accepted.  Class invariants across a group.  Vacuity controls: `assert false` fails after every accepted example.  An empty pair of types with invariants and no witness across modules: possibly empty, and no unsoundness follows. |
| **M5**: refinement and abstract groups | Refinement into a group from below.  All-abstract groups.  Added invariants (105 plan, section 4.2) on a group member's types. | **Refinement inside a cycle**: `B refines A`, with `A` below the group and `B` in a group with `C`: accepted, and inherited obligations are not re-proved.  `B refines A` with `A` in `B`'s group: rejected.  An abstract import of a group sibling: rejected.  An all-abstract group: accepted.  A group mixing abstract and concrete modules: rejected by the existing rule.  Refining one member of an abstract group does not re-tie the knot (the program of section 3.3, with a group). |
| **M6**: libraries and caches | Groups in `.doo` files.  The group key `L(S)`.  Group nodes for `--resolution-threads`. | A library with a group, used by a client with the option, and rejected without it.  Edit tests in the manner of the 261 design: edit a private method body in one member, and every member is re-resolved and retranslated while unchanged tasks hit the cache; edit a module outside the group, and the group's results stay valid. |
| **M7**: compilation | Each back end, or a clear rejection (section 4.3). | One executable program per back end with a function cycle across three modules. |

The extreme cases asked for are in M1 (three-module cycle, diamond), M2 (cycle
through generics, diamond with a cycle), M3 (dispatch across a group) and M5
(refinement inside a cycle).

## 10. Open questions for the owner

Each has a recommendation, which the note assumes.

1. **Require `--member-invariants`?**  *Recommend yes.*  It keeps cross-module
   recursion out of programs that may declare codatatypes, as issue 395 asks,
   and it saves regrouping the co-recursion analysis.  The cost is that a
   program that only wants mutually recursive modules must also use the refreshed
   type system and the invariant option.
2. **Abstract groups.**  *Recommend allowing them* (section 3.3).  They are
   sound, but they cannot be instantiated as a whole until group refinement
   exists.  Forbidding them in the first version would also be consistent.
3. **Group refinement.**  *Recommend deferring it* to its own design.
4. **Cycles through submodule edges.**  *Recommend keeping them an error* until
   lexical scoping in a cycle has its own argument.
5. **The replacement hole (section 2.4).**  *Recommend a separate issue and a
   default-on fix*, as a soundness fix under `AGENTS.md`.  It is independent of
   this feature, and M1 assumes it.
