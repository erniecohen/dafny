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

Code references are to `review/4.11.0` at `1920c844b`.  Every program quoted
here was run on a build of that commit with Z3 5.1.0, with default options and
with `--type-system-refresh`.  Results under `--modular-termination` are from
the same build, with `--type-system-refresh --general-traits=datatype`.  The
two existing holes (sections 2.4 and 2.5) and the crash of section 6 were also
reproduced with release `v4.11.0+fcb2042d.review.e8661ac3`.

Each soundness claim below was attacked with a program before it was kept.
Where the attack succeeded the note says so: sections 2.4 and 2.5 are holes in
Dafny as it is, and sections 3.1, 3.2, 4.2 and 6 state the conditions that the
attacks showed to be necessary.

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
| Which edges may form a cycle | Plain imports only (`import`, `import opened`, imports of export sets).  Refinement, abstract-import (`:`) and replacement edges, the binding of an inherited abstract import (`import X = M`), and the edges between a module and its submodules, must still point strictly downward.  A replaceable module is a group of one (section 3.2). |
| Views and analyses | An analysis that restricts a program (variance, the extension condition, call-graph SCCs, cycle checks) reads the declarations of the whole group and ignores export views.  An analysis that grants something (nonempty, equality support, auto-initialization) is read through the views, as today, so a provided-only type grants nothing (section 3.1). |
| Abstract modules | May form a group, but then every member is abstract.  A group never mixes the two kinds (section 3.3). |
| Checking order | Groups in topological order.  Inside a group: all signatures first, then declarations, then bodies.  Every analysis that is per module today becomes per group (section 4). |
| Libraries | A group lies inside one compilation unit.  A `.doo` library can contain groups, but no group can contain both library and client modules (section 4.4). |
| Termination | The metric becomes (group, SCC, decreases).  Calls inside a group are checked like calls inside a module.  The 211 rules apply at group boundaries where they apply at module boundaries today (section 5). |
| Inhabitance | Grounding is computed per group SCC, as a least fixed point, and auto-initialization follows from it.  Equality support keeps its existing rule, per group SCC (section 6). |
| Caches | A group is one resolution node.  Its key covers every module in it (section 7). |
| Option | `--recursive-modules`, default off.  It requires `--member-invariants`, and so `--type-system-refresh`.  A program with a group of more than one module counts as a program with invariants for the rule of issue 395: it may not declare a codatatype (section 8). |

Three findings came out of the experiments for this note and matter beyond it:

- **Merging a cyclic program into one module is already the right check.**
  The "Tie" program of issue 211 proves `false` when its parts are in five
  modules.  With the same declarations in one module, Dafny reports
  "cannot prove termination" at both dispatched calls (section 2.3).  A group
  gets that check.
- **Replaceable modules already close cycles that nothing checks.**  A
  replacement can import a client of the module it replaces.  The run-time call
  graph then has a cycle that the verifier never sees, and a three-module program
  proves `false` with no warning and no audit finding (section 2.4).  This is an
  existing soundness hole, independent of this feature:
  [issue 405](https://github.com/erniecohen/dafny/issues/405).
- **Refinement already skips the termination check of an inherited call that
  becomes recursive.**  A refining module that gives a body to a body-less
  member can close a call cycle through an inherited call.  The check for that
  call is generated and then assumed, as if the base module had proved it.  A
  two-module program proves `false` with default options (section 2.5).  This
  is also an existing hole, and a sibling of
  [issue 404](https://github.com/erniecohen/dafny/issues/404).  It has no issue
  yet.  The refinement rule of this design depends on both being fixed.

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
| Recursion in negative positions | A use of a type left of an arrow is an error if it is in the SCC of the type being defined, in that type's module graph: "a type whose cardinality exceeds itself" (`NameResolutionAndTypeInference.cs:5046-5047`).  The edges between types are same-module only (`AddTypeDependencyEdges`, `CallGraphBuilder.cs:72-82`). | A type of another module is never in the SCC. |
| Functions as values | "cannot use naked function in recursive setting", by `InSameSCC` (`DetectUnsoundFunctionReferencesVisitor.cs:24-29`). | A function of another module is never recursive with the user. |
| Extreme predicates and lemmas | "a recursive call from a least predicate can go only to other least predicates", positivity of recursive calls, and the prefix-call rewriting, all by `InSameSCC` (`ExtremePredicateChecksVisitor.cs:16`, `ExtremeLemmaChecksVisitor.cs:19` and `:29`, `PrefixCallSubstituter.cs:21`). | — |
| Values of datatypes | An edge from a callable to a datatype it constructs, same module only (`CallGraphBuilder.cs:157-163`). | — |
| Trait parents | "trait definitions contain a cycle" (`ModuleResolver.cs:1227`).  `InheritsFromObject` follows parents only inside the module (`ModuleDefinition.cs:1074-1082`). | A parent in another module is finished. |
| Recursion flags, fuel and heights | `IsRecursive` from the module's SCC sizes (`ModuleResolver.cs:272-296`); the fuel of an application by `InSameSCC` (`BoogieGenerator.ExpressionTranslator.cs:1121-1123`); function heights (`BoogieGenerator.SplitExpr.cs:569`); tail recursion (`TailRecursion.cs:31`). | These affect which axioms a proof gets and how code is emitted, not which checks exist. |
| Inferred signatures | The refreshed resolver infers the type of a `const` or the base type of a subset type on demand, and reports "Cyclic dependency among declarations" (`PreTypeResolve.cs:922-947`).  Its state is per module (`preTypeInferenceModuleState`). | The signatures of other modules are complete. |
| Initialization order | A back end sorts the constants of a class by the SCC ids of the module graph (`OrderedBySCC`, `SinglePassCodeGenerator.cs:2088-2100`). | Constants of other modules are initialized already. |
| Modular termination | `ModularTermination.cs` compares modules to decide whether an implementation is foreign. | — |

Every row is an analysis that the feature must move from the module to the
group.  The table was built by searching for every use of `CallGraph`,
`InSameSCC`, `GetSCCRepresentative` and `EnclosingModule ==` in the resolver,
the verifier and the back ends; an implementation should repeat that search,
because a row that is missed is a check that is silently skipped (section 4.2).

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

### 2.3 The same program, one module or five

The Tie program of the 211 plan, as it stands there, in five modules
(`Tr`, `X`, `L`, `Y`, `M`), verifies with `assert false` (8 verified, 0 errors).
With default options the only sign is the warning that issue 211 added: "trait
'T' has {:termination false} and no {:axiom} annotation: calls dispatched
through it are not checked for termination".  With `--modular-termination` it is
rejected with four errors ("the dispatched callee has no 'decreases {:global}'
bound").  The same classes in one module, with either set of options:

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
and neither call is checked, because each crosses a module boundary.  A bodiless
lemma in a replaceable module is not reported as an assumption, because it is
meant to be supplied by the replacement.

The false fact reaches compiled code.  With `Main` changed to
`var a := new int[0]; Bar.K(); print a[0];` the program verifies and `dafny run`
fails with `System.IndexOutOfRangeException`.  The same shape with functions
overflows the stack at run time.  The same shape with types closes a type
cycle with no base case: with `type T(0)` in `Foo`, `datatype U = U(t: Foo.T)`
in `Bar` and `datatype T = T(u: Bar.U)` in `FooImpl`, neither type has a value,
the verifier takes `U` to be auto-initializable, and `var u: Bar.U := *; print u;`
prints `null`.

One back end already has the right check, as an option.  With
`--python-reorder-replacements` the Python back end orders modules after
applying replacements, and reports "Python compilation dependencies contain a
cycle through module ... after applying replacements"
(`PythonCodeGenerator.OrganizeModules`).  That is a compile-time check in one
back end.  The rule belongs in the resolver.

This is [issue 405](https://github.com/erniecohen/dafny/issues/405), which asks
for a default-on fix.  The simplest fix rejects a replacement whose import
closure reaches a module that imports the replaced module.  This design depends
on it: groups are computed on the graph after replacement (section 3.2).

### 2.5 An existing hole: refinement skips termination checks on inherited calls

A refining module inherits the bodies of its base and does not re-prove them.
That is sound for an obligation the base proved.  It is not sound for an
obligation that did not exist in the base.

```dafny
abstract module A {
  function H(n: nat): nat
  function F(n: nat): nat { H(n + 1) }
}
module B refines A {
  function H(n: nat): nat { if n == 0 then 0 else F(n - 1) + 1 }
  lemma Bad() ensures false { assert H(1) == F(0) + 1; assert F(0) == H(1); }
}
// 3 verified, 0 errors, with default options
```

In `A`, `H` has no body, so the call `H(n + 1)` in `F` is not recursive and has
no termination check.  In `B` the new body of `H` calls `F`, so both calls are
in one SCC: `H(1) = F(0) + 1 = H(1) + 1`.  The new call is checked and passes.
The inherited call is not checked.  The same two functions in one module get
"cannot prove termination".

The same happens in every form tried:

- lemmas, with no assumption anywhere (`lemma L(n: nat) requires n > 0 ensures
  false` given a body `{ K(n - 1); }` in the refinement, where the base has
  `lemma K(n: nat) ensures false { L(n + 1); }`): it verifies, `dafny audit`
  reports 0 findings, and a `Main` that calls `K(0)` and then reads `a[0]` of
  an empty array fails at run time with `System.IndexOutOfRangeException`;
- compiled methods: it verifies, and `dafny run` overflows the stack;
- dynamic dispatch: the inherited call goes through a trait of the base, and
  the override is a class that is new in the refinement;
- a base that is not abstract, with a body-less `ghost function`.

`--modular-termination` does not change any of these results.

**Cause.**  The check is generated, because `InSameSCC` is true in `B`
(`BoogieGenerator.ExpressionWellformed.cs:902`, `Statements/BoogieGenerator.TrCall.cs:133`).
It is asserted at the origin of the call, and `BoogieGenerator.Assert` turns an
assert at an inherited origin into an assume (`BoogieGenerator.cs:3712-3716`).
The one exception, `ForceCheckOrigin`, covers a call that is inherited when
every `decreases` expression of the caller is new
(`BoogieGenerator.Decreases.cs:114-117`).  It does not cover a call that was not
recursive in the base.  In the Boogie output `CheckWellformed$$A.__default.F`
has an implementation and `CheckWellformed$$B.__default.F` has none.

**What is not affected.**  The resolver's checks run again on the refining
module and do catch a cycle that a refinement closes: a constant cycle, a
constraint cycle through a subset type, a naked function reference, a least
predicate that reaches itself through an ordinary predicate, and a datatype that
reaches itself left of an arrow are each rejected in the refining module with
the error they get in one module.  New calls in the refining module are
checked.  Only a check that the verifier makes at an inherited position is lost.

**Fix.**  Assert the termination check at an inherited call unless the base
proved the same check.  It did so only if caller and callee were in one SCC of
the base's call graph with the same `decreases` clauses.  The simplest sound
rule asserts every termination check in a refining module.  A cheaper one skips
the check only when the SCC, with every member and every edge in it, is
inherited unchanged.  Like issue 405 this is a default-on soundness fix with a
hidden `--legacy-*` opt-out, and it has no issue yet.

This matters to the design for two reasons.  The refinement rule of section 3.2
rests on it.  And it is the same mistake a group implementation could make:
an obligation that depends on the SCC structure cannot be inherited from a
context in which the SCC was smaller.

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

- `Fᵢ` is monotone if no type of the group occurs left of an arrow in the
  unfolding of a type of its own SCC.  Dafny checks this in two parts.  A type
  parameter must be used according to its declared variance; that check is per
  declaration, and it already works through a generic type of another module,
  even one that is only provided.  A use of a type left of an arrow must not be
  in the SCC of the type being defined; that check reads the call graph of the
  enclosing module (section 2.2), and **is blind to a cycle through another
  module unless it reads the group's graph**.  With per-module graphs,
  `A.T = T(f: B.U -> bool)` and `B.U = U(t: A.T)` would be accepted, and they
  define a type whose cardinality exceeds itself.  So the type-dependency edges
  and this check move to the group, and M2 has a test for exactly this pair.
- `Invᵢ` must not refer to the extension of any type that depends on `Tᵢ`.
  That is the extension condition of the 105 plan (its section 3.3).  Across a
  group, "depends on" is computed over the group: if `A.T` has a component of
  type `B.U` and `B.U` one of type `A.T`, each is dependent on the other, and
  the cone of `A.T`'s invariant may not quantify over `B.U` without a
  collection bound.  This is the 105 condition on the flattened group, not a
  new one.  It is computed from the declarations, not from what the importer
  can see: if `B` only provides `U`, `B.U` is still `T`-dependent, and a
  function of `B` whose body `A` cannot see is still in the cone.

The 105 plan's model construction (its section 5.2) orders types "so that each
comes after the types whose extensions its cone refers to" and builds values by
structural stages.  Neither step mentions modules.  It applies to the flattened
group unchanged, and so its consistency argument does too.

**Why one fixed point, and not one per module.**  A cycle has no
stratification.  A per-module meaning would have to take the other modules'
types as parameters, and closing the loop is the joint fixed point anyway
(Bekič's lemma: the least solution of a system can be computed one variable at
a time, with the others' least solutions substituted).  The lemma is about the
meaning.  It does not by itself justify checking the modules separately; that
argument is in section 4.2.

**Export views.**  A module of a group sees the other modules through their
export sets, as today.  A type that is only provided is opaque to the importer:
its components and its invariant are hidden (the 105 plan's rule: the invariant
is exported exactly when the type is revealed).  Hiding removes facts from a
proof's context.  It does not change the meaning of the group.

Whether hiding is harmless depends on what the hidden fact is used for, and
the rule has two halves:

- **An analysis that restricts** ignores views and reads the declarations of
  the whole group.  These are the analyses whose answer "no cycle here" permits
  something: recursion in negative positions, the extension condition and its
  cone, the call-graph SCCs behind every termination check, constraint and
  constant cycles, naked function references, the rules for extreme
  predicates.  If one of them stopped at a view, hiding a declaration would
  hide the cycle.
- **An analysis that grants** is read through the view, as today.  An importer
  that sees only `type U` provided takes `U` to be possibly empty and without
  equality, whatever its definition; a datatype with a component of such a
  type is possibly empty for that importer.  The view grants less than the
  truth, never more.

Both halves hold in Dafny today across acyclic imports.  The second was
checked for this note: a provided-only type that is in fact nonempty and
supports equality gets "out-parameter ... might be uninitialized" and "== can
only be applied to expressions of types that support equality" in the importer,
and a datatype of the importer with a component of that type is possibly empty
too.  The first is new work, because today nothing can close a cycle through a
view.

Export sets already keep specifications usable across a view: a `decreases` or
`requires` clause may only name what its export set also exports ("This export
set is not consistent").  So a caller in another module of the group can always
state the measure of the function it calls.

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

  That rule is necessary and not sufficient.  With the base below the group,
  the refining module's inherited bodies are still in the group's call graph,
  and a call that was not recursive in the base can be recursive in the group:
  the base's `F` calls its body-less `H`, the refining module gives `H` a body
  that calls a sibling `C.G`, and `C.G` calls `F`.  This is the hole of
  section 2.5 with one more module.  So the design needs that hole fixed, and
  the fix stated for groups: **an inherited termination check may be assumed
  only if its SCC in the group's call graph, with every member and every edge,
  is inherited unchanged from the base.**  An SCC that contains a declaration of
  another module of the group never qualifies.  The same goes for the override
  checks of [issue 404](https://github.com/erniecohen/dafny/issues/404).
- **Abstract imports.**  `import X : I` means "for an arbitrary refinement of
  `I`".  That presupposes a finished `I`, and the resolver builds `X`'s
  signature by copying `I`'s.  So: **the target of an abstract import must be in
  a strictly lower group.**
- **Bindings of abstract imports.**  A refining module may bind an inherited
  abstract import: `import X = M`, where the base has `import X : I`.  The
  edge to `M` looks like a plain import, and it is not one.  The inherited
  bodies were proved against `I`, their calls into `X` are inherited, and the
  translator drops the precondition checks of an inherited call into another
  module (`Statements/BoogieGenerator.TrCall.cs:379`).  If `M` were in the
  refining module's group, those calls could be recursive in the group with
  nothing checked:

  ```dafny
  module I { function H(n: nat): nat }
  abstract module User { import X : I  function F(n: nat): nat { X.H(n + 1) } }
  module Impl refines I { import UserImpl  function H(n: nat): nat { if n == 0 then 0 else UserImpl.F(n - 1) + 1 } }
  module UserImpl refines User { import X = Impl }
  // today: Error: module definition contains a cycle
  ```

  Today the cycle `Impl -> UserImpl -> Impl` is an error.  Under this design
  both of its edges would pass for plain imports unless the binding is singled
  out.  So: **the module bound to an inherited abstract import must be in a
  strictly lower group than the module that binds it.**  This is the rule of
  M4b section 5.6 ("reject cyclic concrete bindings"), and the functor pattern
  it protects works today with the binding pointing downward.
- **Replacement.**  A replacement edge is a refinement edge for the purpose of
  verification, and at run time it redirects every edge into the replaced
  module.  Groups are computed on the graph with those edges added: for a
  replacement `R` of `F`, every edge `C -> F` from a module `C` other than `R`
  gives an added edge `C -> R`.  **An added edge may not lie on a cycle.**  The
  clients of `F` were verified against `F`'s contract, which carries no
  termination bound, so a cycle through an added edge is a cycle that nothing
  checked.  On today's acyclic graphs this is exactly the rule of issue 405:
  `R` may not depend on a module that depends on `F`.

  One consequence needs its own error message.  A replacement inherits the
  imports of the module it replaces.  If a replaceable `F` were in a group with
  `G`, then `G -> F` gives the added edge `G -> R`, and `R` imports `G` by
  inheritance, so the added edge lies on a cycle.  So: **a replaceable module
  is a group of one.**  A later design could allow replacement inside a group
  with 211-style global bounds on replaceable members.
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
   plain import, and none of them is the binding of an inherited abstract
   import or an edge added for a replacement.  Otherwise report the cycle as
   today, with a message that names the edge that may not be part of a cycle.
   With the option off, every non-trivial SCC is reported exactly as today.
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
   - run the per-group analyses, which are all the rows of section 2.2: the
     call graph and its SCCs, recursion in negative positions, datatype
     dependencies, grounding, equality support, auto-initialization, constraint
     and constant cycles, naked function references, the rules for extreme
     predicates, and, with invariants, the 105 cone analysis, the extension
     condition and raw-member inference;
   - resolve bodies, which needs only signatures and can stay per declaration.

   The first and third steps are the real work.  Today `ModuleResolver` runs
   them for one module and publishes a signature that importers then read.  A
   group needs the signatures of all its members before any member's types
   are resolved.

Two points keep this order well defined.

- **A view of a provided-only type assumes nothing.**  It can be published
  before the group analyses run, because it says "possibly empty, no equality"
  whatever they find (section 3.1).  A view of a revealed type carries its
  declaration, and the characteristics that follow from it (nonempty,
  equality support, auto-initialization) are results of the group analyses.
  No member may read them before those analyses are done.  Today a module
  reads them from a finished import; in a group that read must be an error in
  the implementation, not a default answer, because a default of "nonempty"
  is the greatest fixed point of section 6.
- **Not every signature is written.**  The type of `const c := e`, and the base
  type of `type S = x | P(x)`, are inferred from an expression.  The refreshed
  resolver infers them on demand and reports a cycle ("Cyclic dependency among
  declarations: a -> b -> a").  Its state is per module, and becomes per
  group, so that a constant of `A` whose type is inferred from a constant of
  `B`, and back, gets that error and not an unresolved proxy.  The legacy
  resolver has no such mechanism ("the type of this const is underspecified"),
  which is one more reason the feature requires `--type-system-refresh`.

### 4.2 Verification

Verification stays per module and per declaration.  The translator already
works one module at a time, with a visibility scope made from the module's own
declarations and the signatures it imports (`BoogieGenerator.cs:131-145`).
Inside a group the imported signatures include modules that import this one
back.  What a module's translation needs from the group is the result of the
group analyses: which calls are recursive, the SCC representatives, and which
members are raw.  These are fixed before any member is translated.

**Why this is sound.**  Let `P` be a group and `P♭` the same declarations in
one module.  `P♭` is an ordinary Dafny program, and its soundness is the
existing argument (with the 105 plan's, for invariants).  The group is sound if
verifying `P` establishes everything that verifying `P♭` would.  That holds
under three conditions, and each of them has a way to fail that was found by
experiment.

1. **The resolver accepts `P` only if it accepts `P♭`.**  Every restricting
   analysis gives the answer on `P` that it gives on `P♭`.  This is the first
   half of the views rule (section 3.1) together with the table of section
   2.2: an analysis left per module accepts a program that `P♭` rejects.
   The pair of types in section 3.1 is the example.
2. **Every obligation of `P♭` is an obligation of `P`, and is asserted.**
   Which obligations exist depends on the SCCs: a call has a termination check
   only if `InSameSCC` says it is recursive.  So `InSameSCC` must answer for
   the group.  And the obligation must be asserted, not assumed.  Dafny assumes
   an obligation in one situation, an inherited position in a refining module,
   and section 2.5 shows what happens when the SCC there is larger than in the
   base.
3. **Each obligation of `P` is proved from no more than `P♭` provides.**  A
   member's proof context is its own declarations plus the views of its
   imports.  A view hides definitions and grants no characteristic that the
   declaration lacks (the second half of the views rule), so the context is
   the context of `P♭` with facts removed, or with a type made abstract.  A
   proof from less is a proof.  This is the existing argument for export sets.
   It does not use the direction of the import.

One more fact carries the function axioms across.  The definition axiom of a
function is guarded by its `canCall` predicate
(`BoogieGenerator.Functions.cs:464-491`), and that is consistent whenever the
function's recursion is well founded.  In `P`, a member's Boogie program
contains the definition axioms of functions in modules that import it back.
They are the axioms `P♭` would contain, and they are justified by the same
termination checks, made on the same SCCs (condition 2).  No axiom's
justification refers to the order of the modules.

Each obligation is still proved once, in its own module.  With the three
conditions, separate verification of the members of a group is separate
verification of the declarations of one module, which is what Dafny does
today.

The recursion flags, the fuel of a function application and the function
heights (section 2.2) are also read per group.  They decide which form of an
axiom a proof gets, and getting them wrong costs proofs or loops the solver.
They do not decide whether a check exists.

### 4.3 Compilation

Compilation needs a check per back end.  Some targets map a Dafny module to a
unit that may not import another cyclically; Go packages are the clearest
case.  The compilation milestone must test each back end, and reject a group
for a target that cannot express it, with a clear error, until that target
emits the group as one unit.  Verification does not wait for this.

Imports are not the only thing to test.  Initialization order is the other.
A constant of one module may be defined from a constant of another, and inside
a group the dependencies between constants need not follow any order of the
modules.  The constant cycle check keeps the dependency graph of the constants
acyclic, so a correct order exists, but a back end that initializes the static
fields of one unit eagerly, in a fixed order of units, may not find it.
`OrderedBySCC` sorts the constants of a class by the module's graph, and would
sort by the group's.  The C# and Python back ends were tested for this note
and do not depend on the order: each emits a static constant as a getter that
is evaluated on use, and a chain of constants that alternates between classes,
and between modules, prints the right values.  The Go, JavaScript and Java
back ends were not tested.  So the compilation tests must run their programs,
and must include constants that depend on each other across the group.

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
"group".  It needs two properties.

The first: the group call graph has every edge that can occur at run time
between members of the group.  The edges Dafny adds today are:

- direct calls, including calls in specifications and in default-value
  expressions;
- override edges, from the overridden member to the override;
- a reference to a function as a value (which is then an error inside its own
  SCC: "cannot use naked function in recursive setting");
- the type-dependency edges of `AddTypeDependencyEdges`
  (`CallGraphBuilder.cs:72-82`), from a declaration to each type it names,
  which bring in the constraints and witnesses of those types;
- edges from a callable to a datatype it constructs (`:157-163`).

The last two are restricted to one module today, and that test becomes one
group.  A lambda adds no node: a call in its body is a call of the enclosing
callable and is checked there, so a cycle through an eta-expanded function gets
"cannot prove termination" in the lambda's body.  A call through a function
value of unknown origin is not an edge, in a group as in a module; the checks
on naked function references, and under `--modular-termination` rule 6 of the
211 plan, are what cover it.

The second: every check on an edge inside an SCC is asserted.  A check that is
generated and then assumed is no check (section 2.5), and the rule of section
3.2 for inherited positions is what guarantees this in a group.

**Decreases clauses across modules.**  A recursive call into another module
compares the callee's `decreases` expression with the caller's.  Today that
never happens across modules.  The callee's `decreases` clause is part of its
specification, and the check is translated with the callee's expression, as
for a call inside a module.  The caller can always state it: an export set
that provides a function must also export whatever the function's `decreases`
and `requires` clauses name (section 3.1).  If the expression uses a function
whose body the caller's module cannot see, proving the decrease may need facts
the caller does not have.  That costs completeness, not soundness.

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
3.2 above keeps abstract imports and their bindings pointing downward, which
agrees.

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
  section 5.3, through a module boundary.  It is not hypothetical: the type
  form of the replacement hole (section 2.4) is this mistake made today, and it
  yields a value of an empty type at run time.
- **It is the declarations that are grounded, and the views that are asked.**
  The fixed point runs over the declarations of the group, whatever is
  exported.  What a member may then assume about another member's type is what
  its view says: for a provided-only type, nothing (section 3.1).  So `A` may
  find its own datatype possibly empty because a component is a provided-only
  type of `B` that is in fact nonempty.  That is today's behaviour across an
  import, and it is the safe side.
- **Equality support is not a least fixed point.**  A datatype supports
  equality unless something in it does not.  `DetermineEqualitySupport` marks a
  whole SCC as never supporting equality if any member has a ghost constructor,
  a ghost component, or a component whose type surely lacks equality, and
  otherwise makes it depend on type arguments (`ModuleResolver.cs:3172-3252`).
  That is correct for inductive datatypes, where equality is structural, and
  it stays as it is, per group SCC, with one addition: a component whose type
  the declaring module sees only as provided counts as lacking equality, as it
  does today.  Refusing equality is always safe.  Only grounding has the
  vacuity hazard.
- **An empty cycle is a warning, not an error**, in a group as in a module
  ("because of cyclic dependencies among constructor argument types, no
  instances of datatype 'T' can be constructed", `ModuleResolver.cs:1531-1537`).
  The program stays sound because every use that needs a value is rejected by
  definite assignment.  One use crashes instead: a local variable of such a
  type, with or without an initializer.
  `datatype T = T(t: T)  method M() { var u: T; }` overflows the stack in
  `Type.GetAutoInit`, which follows the grounding constructor's formals with no
  visited set for inductive datatypes (`Types.cs:633-634`).  This is an
  existing crash, with no issue yet.  The M2 test for an empty cycle across
  modules would hit it, so it is fixed first (P3 in section 9).
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
- **Auto-initialization** (`GetAutoInit`, the `(0)` and `(00)` characteristics)
  follows the grounding constructor, so it takes its answer from the grounding
  fixed point of the group.

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

Every milestone is one pull request behind `--recursive-modules`, except the
three prerequisites, which fix Dafny as it is.
Every milestone shows option-off equality of verdicts and resource counts on
the suite and the standard library, and has a `assert false` control wherever
a positive test relies on a new rule.  Proof cost is compared in resource
units.

| Milestone | Content | Tests |
|---|---|---|
| **P1** ([issue 405](https://github.com/erniecohen/dafny/issues/405), default-on soundness fix) | Reject a replacement whose imports reach a client of the replaced module (section 2.4), with a hidden `--legacy-*` opt-out. | The program of section 2.4, rejected; its function form and its type form, rejected; the same through two intermediate modules, rejected; a replacement that imports an unrelated module, accepted. |
| **P2** (needs an issue; default-on soundness fix) | Assert the termination check at an inherited call unless its SCC is inherited unchanged (section 2.5), with a hidden `--legacy-*` opt-out.  [Issue 404](https://github.com/erniecohen/dafny/issues/404) is its sibling and is also needed before M5. | The programs of section 2.5, each rejected with the error it gets in one module; a refinement that adds no body to the SCC, accepted with unchanged resource counts; a refinement whose new call decreases, accepted. |
| **P3** (needs an issue; crash) | `Type.GetAutoInit` terminates on an empty cyclic datatype (section 6). | `datatype T = T(t: T)  method M() { var u: T; }`: the warning and a definite-assignment error on use, no crash. |
| **M1**: groups, reported | The option, its dependency on `--member-invariants`, the `.doo` record.  Compute groups after replacement edges.  Cycles through refinement, abstract-import, replacement or submodule edges are errors that name the edge.  Groups are listed by `--show-hints`.  A group of more than one module is still rejected: "not yet supported". | Option off: the cycle messages of section 2.1 unchanged.  A two-module cycle and a three-module cycle (`A -> B -> C -> A`) reported as one group.  A diamond (`D` imports `B` and `C`, both import `A`) is four groups of one.  A cycle through `refines`, through `import X : I`, through a submodule: each an error naming that edge.  A cycle through the binding of an inherited abstract import (the program of section 3.2): an error naming the binding.  A replaceable module in a cycle: an error.  A replacement whose added edge closes a cycle with a group: an error.  A cycle together with a codatatype: rejected by the issue 395 rule. |
| **M2**: groups without invariants | Group resolution in phases (section 4.1).  Every analysis of section 2.2 per group: call graph, `InSameSCC`, type-dependency and datatype edges, recursion in negative positions, naked function references, extreme predicate rules, datatype dependencies, grounding, equality support, auto-initialization, constraint and constant cycles, inferred signatures, recursion flags, fuel and heights. | Mutually recursive datatypes across two modules, with functions over them that verify.  A three-module function cycle with a wrong `decreases` in the middle module: the error is at that call.  A cross-module cycle of datatypes with no base case: the warning of section 2.2 and the definite-assignment error.  A constant cycle across modules: error.  **Cycle through generics**: `A.List<X>`, `B.Tree = Node(A.List<B.Tree>)`, and `A.Forest = Forest(B.Tree)`, so that `A` and `B` form a group through a type argument.  **Diamond with a cycle**: `B` and `C` import each other and both import `A`, and `D` imports both.  Provided-only types across a group: a proof that needs the hidden definition fails, and one that does not, verifies.  Opened imports in both directions, with a name clash that is an ambiguity error.  **One negative test per row of section 2.2**, each a program that is rejected in one module and must be rejected with the same error when split over two: `A.T = T(f: B.U -> bool)` with `B.U = U(t: A.T)` ("cardinality exceeds itself"); a naked reference to a function of the other module inside a cycle; a least predicate that reaches itself through an ordinary predicate of the other module; a subset-type constraint cycle; a trait-parent cycle; constants whose inferred types depend on each other ("Cyclic dependency among declarations").  Each of these also with the declaration in the middle hidden by a provides-only export: still rejected (the views rule).  A cycle through an eta-expanded function of the other module: "cannot prove termination". |
| **M3**: traits in a group | Cross-module `extends` inside a group without `{:termination false}`.  `ModularTermination` compares groups. | The Tie program with `Tr`, `X`, `Y`, `L` in one group: "cannot prove termination" at both dispatched calls, as in section 2.3.  The same with a bound that decreases: verifies.  Tie with `X` outside the group: the 211 rules apply, with and without `--modular-termination`. |
| **M4**: invariants across modules | Depends on stages 1 to 4 of the 105 plan.  The cone analysis, the extension condition and raw-member inference over a group, and the per-function summaries for lower groups and libraries. | Datatype invariants whose types are mutually recursive across two and three modules, each invariant calling a function of another module.  The `Liar` program of the 105 plan split over two modules: rejected.  An invariant whose cone quantifies over a type of another module of the group that depends on it: rejected; over one that does not: accepted.  Class invariants across a group.  Vacuity controls: `assert false` fails after every accepted example.  An empty pair of types with invariants and no witness across modules: possibly empty, and no unsoundness follows. |
| **M5**: refinement and abstract groups | Refinement into a group from below.  All-abstract groups.  Added invariants (105 plan, section 4.2) on a group member's types. | **Refinement inside a cycle**: `B refines A`, with `A` below the group and `B` in a group with `C`: accepted, and inherited obligations whose SCC is inherited unchanged are not re-proved.  The same with a body in `B` that closes a cycle through `C` and an inherited call: the inherited call's termination check is asserted, and fails when the measure does not decrease.  `B refines A` with `A` in `B`'s group: rejected.  An abstract import of a group sibling: rejected.  An all-abstract group: accepted.  A group mixing abstract and concrete modules: rejected by the existing rule.  Refining one member of an abstract group does not re-tie the knot (the program of section 3.3, with a group). |
| **M6**: libraries and caches | Groups in `.doo` files.  The group key `L(S)`.  Group nodes for `--resolution-threads`. | A library with a group, used by a client with the option, and rejected without it.  Edit tests in the manner of the 261 design: edit a private method body in one member, and every member is re-resolved and retranslated while unchanged tasks hit the cache; edit a module outside the group, and the group's results stay valid. |
| **M7**: compilation | Each back end, or a clear rejection (section 4.3). | Per back end, programs that are run and whose output is compared: a function cycle across three modules; constants that depend on each other across the group, in an order that follows no order of the modules; mutually recursive datatypes across modules, constructed and printed. |

The extreme cases asked for are in M1 (three-module cycle, diamond), M2 (cycle
through generics, diamond with a cycle), M3 (dispatch across a group) and M5
(refinement inside a cycle).  M1 depends on P1, M2 on P3, and M5 on P2 and
issue 404.

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
5. **The replacement hole (section 2.4).**  Filed as
   [issue 405](https://github.com/erniecohen/dafny/issues/405).  *Recommend a
   default-on fix*, as a soundness fix under `AGENTS.md`.  It is independent of
   this feature, and M1 assumes it.
6. **The refinement hole (section 2.5) and the `GetAutoInit` crash (section
   6).**  Both are reproduced, both are independent of this feature, and
   neither has an issue.  *Recommend filing both*, the first with the label
   `soundness` and a default-on fix, before any work on M5 and M2.
7. **A replaceable module in a group.**  *Recommend rejecting it* in the first
   version (section 3.2).  Allowing it needs global bounds on replaceable
   members, which is a design of its own.
