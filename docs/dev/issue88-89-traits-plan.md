# Plan: adding parents in a refinement (issue 88), and inherited trait axioms (issue 89)

Issues: https://github.com/erniecohen/dafny/issues/88 (upstream issue 1471) and
https://github.com/erniecohen/dafny/issues/89 (upstream issue 1466).
Related plans: https://github.com/erniecohen/dafny/pull/380 (issue 211, cross-module
termination) and https://github.com/erniecohen/dafny/pull/384 (issue 105, member invariants).
Base: `review/4.11.0` at `1920c844b`.

This is a plan only. It changes no product file.

Soundness bugs found while writing it: https://github.com/erniecohen/dafny/issues/404 is
filed. Section 2 describes four more, with reproducers. No issue is filed for those yet
(section 10, question 2).

## 0. Summary

| | Issue 88: `trait T ... extends S` | Issue 89: unimplemented trait axioms |
|---|---|---|
| Well-posed? | Yes. | Yes for ghost members. Not for compiled members, which have nothing to run; `{:extern}` already covers those. |
| Sound? | Yes, if a refining module checks every obligation that its changes create. **Today it does not.** Refinement proves `false` under default options in four ways, and verifies a program that fails its own assertion in a fifth, all without the new syntax (section 2). Four of the five are reachable through the new syntax. | Yes, relative to the declared axioms. It adds no deductive power: Dafny already accepts the same trust spelled `{:extern} {:axiom}`, for lemmas and for ghost functions. The trusted premise is the member's signature and specification, not its `ensures` clause alone. |
| Option | `--refinement-extends`, opt-in | `--inherited-trait-axioms`, opt-in |
| Prerequisite | Stage 0, a default-on soundness fix (section 3) | none |

The two features are independent, apart from one small interaction (section 6.3).

## 1. What Dafny does today

All experiments in this document were run with release
`v4.11.0+fcb2042d.review.4aa006a8` and Z3 5.1.0. That release is the base plus one
commit, whose only product change is in `Source/DafnyCore/AST/VisibilityScope.cs`. Every
program was
run with default options and with `--type-system-refresh`, and the two agreed. The
programs of section 6.1 were also run with `--modular-termination`.

This section lists what bears on the two features. Section 2 lists the soundness bugs.

| # | Program (abridged) | Result |
|---|---|---|
| e1 | the reproducer of issue 88 | parse error `lbrace expected` |
| e2 | the reproducer of issue 89 | `class 'C' does not implement trait lemma 'T.foo'` |
| e3 | trait lemma `foo() ensures false { assume {:axiom} false; }`, class `C extends T` with no `foo` | accepted; `c.foo()` proves `false` |
| e4 | trait lemma `{:extern} foo() ensures false` with no body, class with no `foo` | accepted with a warning that `{:axiom}` silences; proves `false` |
| e5 | trait lemma `{:axiom} foo() ensures false`; class overrides it with `ensures false { }` | override rejected: the trait's `{:axiom}` member is checked today, as an interface |
| e6 | `lemma NotS(c: C) ensures !((c as object) is S)` where `C` does not extend `S` | not provable |
| c3 | `t is S` for unrelated traits `T` and `S`, under `--general-traits=datatype` | resolver error: no negative parent fact can even be stated |
| e8 | e4 with `{:extern} {:axiom}` | accepted silently; proves `false` |
| e9 | bodiless `ghost function {:axiom}` in a trait, class with no `f` | `class 'C' does not implement trait function 'T.f'` |
| a9 | e9 with `{:extern} {:axiom}` | accepted: a ghost function axiom is inherited today |
| b6 | `ghost function {:extern} {:axiom} f(): E` in a trait, where `E` is an empty type | accepted; proves `false` from the signature alone |
| e13 | `dafny audit` of a trait with a bodiless `{:axiom}` lemma that classes override | the trait lemma is listed as an axiom |
| e16 | `{:extern} {:axiom}` lemma in a generic library trait, inherited by a client class at `T<int>` that contradicts it | accepted, proves `false`; `dafny audit` lists the axiom only at the trait |
| b1 | subtrait `U extends T` redeclares interface lemma `foo` as `{:extern} {:axiom}` with `ensures false`; class `C extends U` with no `foo` | accepted, proves `false`; audit lists `U.foo` |
| b2 | subtrait redeclares an `{:extern} {:axiom}` lemma as a plain bodiless lemma; class below with no `foo` | accepted, with the bodiless-extern warning at the subtrait's lemma |
| d1 | `T1` and `T2` both redeclare `T.m`; `class C extends T1, T2` | error: `C` inherits `m` from both |
| b3 | an axiom reached by two paths through a generic trait | inherited once |
| b12 | `{:axiom}` on a lemma that has a body | the body is still verified |
| b14 | a refinement gives a bodiless trait lemma a body; a class in the base overrides it | accepted; the new body is verified |
| b16 | the same for a function | existing error: "fully defined function ... is not allowed to be re-declared" |
| a10, b15 | a refinement adds `ensures false` to an inherited `{:axiom}` lemma | accepted, proves `false`; audit lists the base and the refining declaration |
| a8 | one generic trait as a parent at two instantiations | existing error |

### 1.1 Issue 88: only the grammar stands in the way

- The grammar offers `extends` **or** `...`, not both: `Dafny.atg:541-543` for classes
  and `Dafny.atg:592-594` for traits. That is the whole parse error of e1.
- The refinement merger already accepts parents on a refining declaration.
  `RefinementTransformer.MergeClass` appends the base's parents to the refining
  declaration's own list (`RefinementTransformer.cs:672`). A refining declaration
  without `...` is rejected earlier (`RefinementTransformer.cs:224-233`).
- Adding parents in a refinement is already possible by one route: an abstract type
  refined by a class or trait that has `extends` (`RefinementTransformer.cs:309-327`).
  Section 2 uses this route to show what the new syntax would expose.
- The checks that matter run on the merged list after the merge: trait cycles
  (`ModuleResolver.cs:1225-1228`), the cross-module extension rule
  (`ModuleResolver.cs:2398-2418`), name clashes between parents
  (`ModuleResolver.cs:2581`), members that must be implemented and override
  compatibility (`ModuleResolver.cs:2711-2775`), and whether a trait is a reference
  type (`ModuleDefinition.cs:1087`).
- The encoding has only positive facts about parents: one axiom
  `implements$J(class.C, ...)` per declared parent (`BoogieGenerator.Methods.cs:486-502`)
  and `$Is(o, J?) <==> o == null || implements$J(dtype(o))`
  (`BoogieGenerator.Methods.cs:131-137`). Nothing says a type is *not* below a trait.
  e6 confirms that no module can prove it, and c3 that the resolver rejects the test
  between unrelated traits. So adding a parent never falsifies a fact that was provable
  before. Section 4.3 builds on this.

### 1.2 Issue 89: two exemptions already exist

`ProcessInheritedTraitMembers` (`ModuleResolver.cs:2711-2746`) requires every class to
redeclare each bodiless trait member it inherits, with two exceptions:

- `{:extern}` members (`ModuleResolver.cs:2736-2738`). With `{:axiom}` added, the
  warning of e4 goes away. That is issue 89's semantics today, spelled with an
  attribute that means something else. It holds for ghost functions too (a9).
- reveal lemmas (`ModuleResolver.cs:2739-2741`). The fork's comment there states the
  hazard: "an unimplemented one would let a call assume its postcondition".

The member that counts is the one the class inherits. It is unique: two parents that
both declare a member of one name are a clash (d1), and a member reached by two paths
is one member (b3).

`dafny audit` already reports a trait's bodiless `{:axiom}` lemma as an axiom
(`Auditor/Assumption.cs:14`), even though e5 shows that today it is checked, not
assumed. Under issue 89 the report becomes accurate.

## 2. Refinement assumes what it did not prove: five bugs

**The mechanism.** For `module M1 refines M0`, `RefinementTransformer` copies `M0`'s
declarations into `M1` as text, and `M1` is resolved from scratch. The translator then
treats every obligation whose source position lies in `M0`'s text as already proved:

- `BoogieGenerator.Assert` emits an assume for it (`BoogieGenerator.cs:3712-3716`), and
  `AssertAndForget` emits nothing (`BoogieGenerator.cs:3737-3741`);
- the precondition of an inherited call is not checked when the callee is in another
  module (`BoogieGenerator.TrCall.cs:377-379`);
- an inherited method postcondition is guarded by `$_reverifyPost`
  (`BoogieGenerator.Methods.cs:1954-1956`, `BoogieGenerator.Obligations.cs:216-254`).
  That flag is false on entry (`BoogieGenerator.Methods.cs:848-849`) and becomes true
  only at a `return` statement that the refinement itself adds
  (`BoogieGenerator.TrStatement.cs:55-60`, `RefinementTransformer.cs:1569`);
- an inherited function postcondition is not checked
  (`BoogieGenerator.Functions.Wellformedness.cs:58`), nor are inherited iterator
  specifications (`BoogieGenerator.Iterators.cs:95` and `:111`,
  `BoogieGenerator.TrStatement.cs:153-158`);
- the escape `MustReverify` (`MethodOrConstructor.cs:34`) is never set to true anywhere.

The transformer does take care of the direct case. When a refinement gives a body to a
member that had none, the member's postconditions are copied as new text and are
checked (a28).

So the test for "already proved" is where the text came from. That test is wrong
whenever an obligation depends on something outside its own text that the refinement
changed, and whenever the text itself changed meaning. Five cases follow. Each program
is accepted under default options and under `--type-system-refresh`, and `dafny audit`
reports nothing for it. Each has a control, the same declarations written in one
module, which Dafny rejects.

### 2.1 Override checks (https://github.com/erniecohen/dafny/issues/404)

```dafny
module A {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { } }
}
module B refines A {
  trait S ... { lemma L... ensures false }
  method M() ensures false { var s: S := new C(); s.L(); }
}
```

`C.L` is inherited, so its override check against the strengthened `S.L` is assumed
(e14; e15 is the function version). The route that issue 88 would widen is a type that
gains a parent (e7; e7c with a function):

```dafny
module A {
  trait S { lemma L() ensures false }
  type T { lemma L() ensures true { } }
}
module B refines A {
  class T extends S { constructor () {} }
  method M() ensures false { var s: S := new T(); s.L(); }
}
```

The same holds with a generic trait (e11), a diamond `class T extends S, P` with
`S extends P` (e12), `A` loaded through `--library` (e10), a non-reference trait and a
datatype under `--general-traits=datatype` (c4), and through an abstract import
(a7, section 4.4).

**Cause.** The override check asserts each parent postcondition at the *overriding*
member's origin: `Assert(m.Origin, ...)` (`BoogieGenerator.Methods.cs:1561`, and
`:1592`, `:1190`, `:1290` for requires and functions). In `B` that origin is inherited
(`RefinementOrigin.cs:22-24`). With no assert left, the override check has no
implementation at all (`EmitImplementation`, `BoogieGenerator.cs:1729-1736`): the
Boogie output of e7 shows `procedure OverrideCheck$$B.T.L` with no `implementation`.

### 2.2 Termination

```dafny
abstract module A {
  lemma G(n: nat) requires n > 0 ensures false
  lemma F(n: nat) ensures false { G(n + 1); }
}
module B refines A {
  lemma G... { F(n - 1); }
}
```

`B` verifies (a25). `B.F` and `B.G` call each other forever, and `F(0)` proves `false`.
a5 is the function version. The route through a parent added by a refinement (a4b):

```dafny
module A {
  trait S { function F(s: S): int }
  type T {
    function F(s: S): int { s.F(s) + 1 }
    lemma Unfold(s: S) ensures F(s) == s.F(s) + 1 { }
  }
}
module B refines A {
  class T extends S { constructor () {} }
  method Bad() ensures false {
    var t := new T();
    var s: S := t;
    t.Unfold(s);
    assert s.F(s) == t.F(s);
  }
}
```

In `B`, `T.F` overrides `S.F`, so `t.F(t)` is `t.F(t) + 1`.

**Cause.** In `A` the call was not recursive: caller and callee were in different
strongly connected components of the call graph, so there was no termination
obligation. In `B` they are in one component, and the obligation is new. But
`CheckCallTermination` asserts it at the call's position
(`BoogieGenerator.Decreases.cs:81`), which is inherited, so it is assumed. The existing
exception, `ForceCheckOrigin` (`BoogieGenerator.Decreases.cs:114-117`), applies only
when every `decreases` expression of the context is new. It does not cover a new cycle.

### 2.3 A captured name changes what was proved

```dafny
module Lib { function f(): int { 0 } }
module A { import opened Lib  lemma L() ensures f() == 0 { } }
module B refines A {
  function f(): int { 1 }
  lemma Bad() ensures false { L(); }
}
```

`B` verifies (a15). In `B`, the inherited text `f()` resolves to `B.f`, so `B.L` now
states `B.f() == 0`, which is false and was never proved. The same happens with

- a member added by a class refinement, which captures a module-level name used in an
  inherited member (a1: `class C ... { function f(): int { 1 } }`);
- a member of a parent added by a refinement, the route of issue 88 (a2: `type T` has a
  lemma about module-level `f`; `class T extends S` where `S` declares `f`);
- a type (a22: `A` proves `u >= 0` for the opened `type U = x: int | x >= 0`; `B`
  declares `type U = int` and calls `L(-1)`).

A captured call changes what a compiled body does, and its precondition and frame are
not checked either. This program verifies and then fails at run time with an index out
of bounds (a30; the one-module control of a29, which is the same shape, is rejected for
the precondition and for the frame):

```dafny
module Lib { method Check(c: object?) { } }
module Tr {
  trait {:termination false} S {
    var a: array<int>
    method Check(c: object?) requires a.Length > 0 modifies a { a[0] := 1; }
  }
}
abstract module A {
  import opened Lib
  import Tr
  type T { method Caller() { Check(null); } }
}
module B refines A {
  class T extends Tr.S { constructor () { a := new int[0]; } }
  method Main() { var t := new T(); t.Caller(); print "done\n"; }
}
```

In `B`, the inherited `Check(null)` is `this.Check(null)`, a method of the added parent.

**Cause.** The inherited text is resolved again in `M1`, where declarations of `M1`
shadow those the text referred to in `M0`. Nothing compares the two resolutions.

### 2.4 Definite assignment of an added field

```dafny
module A {
  class C { constructor () {} }
}
module B refines A {
  type E = x: int | false witness *
  class C ... { var e: E }
  method Bad() ensures false {
    var c := new C();
    var e: E := c.e;
    assert false;
  }
}
```

`B` verifies (a3). `E` is empty, the inherited constructor never assigns `e`, and the
check that it does is emitted at the end of the inherited constructor body
(`BoogieGenerator.DefiniteAssignment.cs:144-152`, `BoogieGenerator.TrStatement.cs:321`),
so it is assumed. A class with no constructor at all is caught by the resolver (a18).
With issue 88 an added parent brings its fields to every class below `T`.

### 2.5 A changed body keeps its old postcondition

```dafny
module A {
  class C { var x: int  constructor () { x := 0; }
    method Set() modifies this ensures x == 1 { x := 1; }
    method M() modifies this ensures x == 0 { x := 0; }
  }
}
module B refines A {
  class C ... { method M... { ...; Set(); } }
  method Main() { var c := new C(); c.M(); assert c.x == 0; print c.x, "\n"; }
}
```

`B` verifies and prints `1` (a26d). The refined body of `M` no longer establishes
`x == 0`, but the postcondition is inherited and so is not checked. With `return;`
added after `Set();` it is checked, and the program is rejected (a27): the transformer
re-verifies postconditions only at returns that the refinement adds. This uses the
deprecated `...;` statement refinement and has nothing to do with parents. It is listed
because it has the same cause, and so the same fix.

### 2.6 What held

These attacks failed, on the same release:

- adding `ensures` to a member that has a body is checked in the refining module, for a
  function, a lemma, a method and a trait lemma (a6);
- giving a body to a bodiless function or method checks the inherited postconditions
  (a28);
- a refinement cannot replace a function body or add a precondition (a20, a24);
- no allocation fact can be proved about a value of an abstract type or of a
  non-reference trait, so a refinement that makes it a class contradicts nothing
  (a19, c5);
- a non-reference trait has no equality, and a datatype cannot extend a reference trait
  (c1, c2).

## 3. The principle, and stage 0

### 3.1 When an inherited obligation may be assumed

Let `M1` refine `M0`, and let `σ` map each declaration of `M0` to its copy in `M1`.
Assuming an inherited obligation in `M1` is sound when three conditions hold.

- **C1, same meaning.** The inherited text denotes in `M1` the `σ`-image of what it
  denoted in `M0`: every name in it resolves to the copy of the declaration it resolved
  to in `M0`, and every inferred type is the copy of the type inferred in `M0`.
- **C2, the hypotheses only grew.** Every axiom that `M0`'s proof could use has its
  copy among `M1`'s axioms.
- **C3, it was proved.** `M0` had this obligation, at this site, against the same
  declarations, and proved it.

C2 holds of refinement as it is designed: a refinement gives bodies, definitions and
further postconditions, and it may not replace a function body or add a precondition
(a20, a24). Section 4.3 shows that an added parent keeps C2. The bugs are failures of
the other two:

| Bug | Fails | Why |
|---|---|---|
| 2.1 override checks | C3 | the obligation depends on which member is overridden and on its specification |
| 2.2 termination | C3 | the obligation exists only if caller and callee share a component of the call graph |
| 2.3 name capture | C1 | the text means something else |
| 2.4 definite assignment | C3 | the obligation depends on the set of fields |
| 2.5 changed bodies | C3 | the obligation depends on the body |

The origin of a piece of text establishes none of the three.

### 3.2 Stage 0, form A: a refining module is verified in full

No obligation is assumed because of its origin. Every site listed at the head of
section 2 stops looking at the origin: `Assert` and `AssertAndForget` emit the check,
inherited call preconditions are checked, and inherited postconditions of methods,
functions and iterators are checked.

- **Soundness** needs no argument about refinement. `M1` is already resolved from
  scratch, as an ordinary module whose text is the merge. Verifying all of it is
  verifying that module. All five bugs go, including 2.3: text with a captured name is
  proved again under its new meaning, so a15 is rejected at `B.L`, as its one-module
  control is today. This is a consequence of the design. It could not be run, because
  form A does not exist yet.
- **Cost.** Each refining module proves its inherited bodies again. Stage 0 measures
  this on the suite and the standard library, in resource units.
- **Reporting.** An inherited obligation that fails is reported in each module that
  contains it. Test expectations for refinement tests with intended errors change.
- **Stability.** A proof that passes in `M0` can fail in `M1`, where more definitions
  are visible. That is the ordinary brittleness of a proof, now seen where it was hidden.

### 3.3 Stage 0, form B: keep the elision, make it exact

An obligation is identified by its site and by every declaration it is checked against.
It is assumed only if that whole key existed in `M0` and nothing in it changed.

| Obligation | Depends on, besides its own text | Assumed only if |
|---|---|---|
| override check | the overridden member, its specification, its termination bound | the member overrode the copy of the same member in `M0`, and that member's specification is unchanged |
| termination at a call | whether caller and callee share a component; their `decreases` clauses; under issue 211, their bounds and what the callee can reach | all of these are as in `M0` |
| definite assignment at the end of a constructor's first division | the fields of the class | the class and its ancestors gained no field |
| a postcondition, or any check after a changed statement | the body | the body is unchanged |
| a call's precondition and frame | the callee and its specification | the call resolves to the copy of the same callee |
| invariant check at `new;` (issue 105) | the conjuncts of the type | the conjunct was a conjunct of the copy of this type in `M0` |

And C1 becomes a resolver rule: **an inherited name must resolve in `M1` to the copy of
what it resolved to in `M0`**, and an inherited inferred type must be the copy of the
`M0` type. Otherwise it is an error at the capturing declaration, naming the inherited
use. This needs the cloner to keep, for each copied name, the node it was copied from.

Form B keeps today's cost. Its soundness rests on the table being complete. The table
is what this review found in one pass. It is not a proof that no sixth kind exists: the
translator has many assert sites, and each needs the argument.

### 3.4 Recommendation

Form A, as the default-on soundness fix. Its correctness does not depend on having
listed every case. If the measured cost matters, elision comes back one kind of
obligation at a time, each with its key and its argument, as form B describes.

The rest of this plan needs only this from stage 0, in either form:

> **In `M1`, every obligation whose site or whose key is new or changed is checked, and
> no inherited statement is assumed under a meaning it did not have in `M0`.**

## 4. Issue 88: a refinement may add parents

### 4.1 Syntax and meaning

Under `--refinement-extends`:

```dafny
module M0 { trait P {}  trait T extends P { ... }  class C extends T { ... } }
module M1 refines M0 {
  trait S { ... }
  trait T ... extends S { ... }        // M1.T extends P and S
  class C ... extends Q<int> { ... }   // also allowed for classes
}
```

The grammar becomes `[ ExtendsClause | ellipsis [ ExtendsClause ] ]` for classes and
traits. The parents of the refined declaration are the parents of the base, then the
added ones (`MergeClass` already does this). A parent named twice counts once. One
generic trait at two different instantiations is the existing error (a8).

`M1.T` is a new type, distinct from `M0.T`. The feature never changes `M0`. It says
that the type `M1` builds from `M0.T` also lies below `S`.

Adding `S` to `T` changes every type of `M1` below `T`: traits and classes copied from
`M0`, and types that reach `T` through a parent that was itself refined. The rules and
obligations below apply to all of them.

Scope: classes and traits. Datatypes and newtypes may extend traits under
`--general-traits`, and the same rules apply to them. They come in a later stage
(stage 5), because an added parent with an invariant puts obligations on every inherited
conversion site of a value type (section 6.2).

### 4.2 Rules

An added parent is checked as if it had been written in `M1` on a fresh declaration.
Most of that is existing checks, which already run on the merged declaration. R1 is the
only new rule. R4 to R8 say which obligations are new in `M1`, and stage 0 is what makes
them checked.

- **R1. Reference-ness is fixed.** A refinement may not turn a non-reference trait into a
  reference trait. Under `--general-traits` a trait is a reference type iff it extends
  `object` (`ModuleDefinition.cs:1087`). The reason is substitutability: a module that
  imports `M0` abstractly may declare a datatype that extends `T`, and that is legal only
  while `T` is not a reference type (c2). The plan forbids the change instead of arguing,
  fact by fact, what else depends on the representation. The error names the added
  parent. (Under the legacy setting every trait is a reference type, so R1 never fires.)
- **R2. Cycles** are the existing check (`ModuleResolver.cs:1225-1228`) on the merged
  parents.
- **R3. Cross-module parents.** The existing rule (`ModuleResolver.cs:2398-2418`) applies
  to each added parent, with `T`'s module being `M1`. A parent from another module must
  be open (`{:termination false}`), or, without `--modular-termination`, `T` must carry
  `@AssumeCrossModuleTermination`. Both are reported by the default-on warning
  (`ModuleResolver.cs:1102-1135`).
- **R4. Members that become overrides.** A member of `T`, or of a type below `T`, that
  existed in `M0` and has the name of a member of `S` becomes an override of it. That
  override check is a **new obligation** of `M1`, although the member's text is
  inherited (section 2.1). It includes the comparison of `decreases` clauses and, under
  `--modular-termination`, of termination bounds (section 6.1). What may be overridden
  is the existing rule (`ModuleResolver.cs:2764-2773`): a field of `S`, and a function
  of `S` that has a body, may not be redeclared, so a clash with one is an error; a
  method or lemma of `S` may be overridden even if it has a body. A name inherited from
  two parents is the existing clash error (`ModuleResolver.cs:2581`).
- **R5. Classes from the base.** A class of `M0` that extends `T` now extends `S`. It
  must implement `S`'s bodiless members, by the existing check
  (`ModuleResolver.cs:2742-2743`). To do so it is refined in `M1` (`class C ... { ... }`),
  which can already add members. Under issue 89 an `S` axiom needs no implementation.
- **R6. Fields.** The fields of `S` become fields of every class below `T`. Each
  constructor of such a class, inherited ones included, must leave them definitely
  assigned. That is a **new obligation** at the inherited constructor (section 2.4). An
  inherited constructor cannot be given an assignment, so in practice a field of `S`
  whose type is not auto-initializable is an error for a class that keeps an inherited
  constructor, reported at that constructor in `M1`.
- **R7. Calls that become recursive.** A new override adds an edge to the call graph: a
  call dispatched through `S` can now reach the member of `T`. A call in inherited text
  that thereby lies on a cycle has a **new termination obligation** (section 2.2).
- **R8. Names.** The members of `S` come into scope in every member body of `T` and of
  the types below it, and can capture a name that inherited text used for a module-level
  declaration (section 2.3, a2). Under form A the text is re-proved under its new
  meaning, so nothing unproved is assumed. Under form B it is the error of section 3.3.
- **R9. Type parameters.** The added parent's type arguments may use `T`'s type
  parameters. Variance and characteristic checks are the existing ones, run in `M1`.
- **R10. Cardinality admission** for general traits (`docs/dev/GeneralTraitCardinality.md`)
  runs on the merged parents. An added parent adds an inclusion edge, which that
  analysis checks with the others. A test confirms this.

### 4.3 Why it is sound

With stage 0, `M1` is accepted only if every obligation that the added parent creates
is checked in `M1`. Under form A that is immediate: `M1` is verified as the module its
merged text is, so `trait T ... extends S` is exactly as sound as `trait T extends P, S`
written in a fresh module. Under form B it is the three conditions of section 3.1:

1. **C2: nothing proved in `M0` becomes false.** Let `Γ0` be the axioms Dafny emits for
   `M0` and `Γ1` those for `M1`. `Γ1` contains `σ(Γ0)`: an added parent removes and
   weakens no axiom. It adds positive `implements$S` facts, the axioms of `S`'s
   members, and one override axiom per new override. The encoding has no negative
   parent fact, and none can be stated (section 1.1, e6, c3). So every valid
   verification condition of `M0` stays valid, after `σ`, in `M1`.
2. **C3: everything the parent adds is proved in `M1`.** The new facts say that the
   values of `M1.T` are `S` values. Each is backed by an obligation that is new in `M1`,
   wherever its text came from:
   - every override of an `S` member by a member of `T` or of a type below `T` meets
     `S`'s specification, `decreases` clause and termination bound (R4);
   - every class below `T` implements `S`'s bodiless members (R5), or inherits an
     axiom (issue 89, an explicit trusted premise);
   - every constructor below `T` assigns `S`'s fields (R6);
   - every call that the new overrides put on a cycle terminates (R7);
   - every object below `T` satisfies `S`'s invariants (issue 105, section 6.2);
   - every dispatched call through `S` respects its bounds (issue 211, section 6.1).
3. **C1: the inherited text means what it meant** (R8).
4. **The kind of type is unchanged** (R1).

The first draft of this plan argued from point 1 alone that `M0`'s proofs carry over.
That was wrong. Point 1 covers only obligations that `M0` had. The obligations of
point 2 are new, and today Dafny assumes four of them (sections 2.1 to 2.4).

### 4.4 Refinement chains, abstract imports, libraries

- **Chains.** In `M0 <- M1 <- M2`, each refinement may add parents. Stage 0 applies at
  each step: `M2` checks what changed since `M1`.
- **Abstract imports.** Take `abstract module N { import X : M0 ... }` and
  `module N1 refines N { import X = M1 }`. `N1` is a refining module, and what `X`
  denotes changed under it, so `N`'s own declarations get new obligations. a7 shows it
  today, without any added parent:

  ```dafny
  module M0 { trait {:termination false} S { lemma L() ensures true } }
  module M1 refines M0 { trait {:termination false} S ... { lemma L... ensures false } }
  abstract module N {
    import X : M0
    class D extends X.S { constructor () {} lemma L() ensures true { } }
  }
  module N1 refines N {
    import X = M1
    method Bad() ensures false { var s: X.S := new D(); s.L(); }
  }
  ```

  `N1` verifies. `D.L` is inherited from `N`, and its override check against `M1.S.L`
  is assumed. With issue 88, `M1` may also have given `X.T` a parent: a class of `N`
  below `X.T` must then implement that parent's members (an error in `N1` until the
  class is refined), and R4, R6 and R7 apply to it. So an abstract import is covered by
  stage 0, not by C2 alone. Under form B, "unchanged" in the keys of section 3.3 must be
  judged after the import is bound.
- **Substitutability.** A module written against `M0` can be rejected against `M1`: a
  class below `X.T` may clash with a member of the added parent, or lack one. Those are
  errors, as when a refinement adds a bodiless member to a trait today. R1 exists so
  that the kind of the type is not among the things that change.
- **Exports.** Clients see `S` among `T`'s parents exactly when `M1` reveals `T`.
- **Libraries.** A library module may be refined and given parents. The refined module
  is verified in the client, with stage 0. A library trait may be an added parent under
  R3. A `.doo` library whose source uses the syntax needs the option in the client: the
  option is registered as `OptionCompatibility.OptionLibraryImpliesLocalError`, as
  `--allow-axioms` is (`CommonOptionBag.cs:803`).

## 5. Issue 89: trait axioms are inherited

### 5.1 Meaning

Under `--inherited-trait-axioms`, a trait member that

- has no body,
- is marked `{:axiom}`, and
- is a ghost member that is not extreme: a lemma, a two-state lemma, a ghost method, a
  ghost function or predicate, or a two-state function or predicate,

is a **trait axiom**. A class, datatype or newtype that inherits the member and does
not declare it needs no implementation, as for a member with a body. It may still
override it, and then the override is checked as today (e5).

**The trusted premise** is the whole declaration, for every value of the trait:

- for a lemma or ghost method, `forall this: T, x :: Pre(this, x) ==> Post(this, x)`,
  with the heap where it applies;
- for a ghost function, that a function of this signature exists: for all arguments
  that satisfy the precondition it has a value of the result type that satisfies the
  postcondition, and it reads no more than its `reads` clause. b6 shows that the
  signature alone can be inconsistent, with an empty result type.

That is the meaning callers through `T` already assume today. The feature changes only
who is asked to prove it: nobody, rather than each implementation.

**Which declaration decides.** A type is exempt for a member exactly when the
declaration it inherits is a trait axiom. That declaration is unique (section 1.2). A
subtrait may redeclare a member, and the redeclaration has its own status:

- an interface member redeclared as an axiom, perhaps with a stronger specification: the
  types below the subtrait are exempt, and the trust is declared, and audited, at the
  subtrait (b1 shows this today with `{:extern}`);
- an axiom redeclared without `{:axiom}`: it is an interface member again, and classes
  below the subtrait must implement it. (Today's `{:extern}` exemption does not behave
  this way: b2 accepts the class, with a warning. Under the option the test pins the
  rule stated here.)

**The status is fixed where the member is declared**, by the option in force for the
file that declares it. The parser already receives per-file options, and for a `.doo`
library they are the library's own (`ProgramParser.cs:89`, `DafnyFile.cs:300-317`). So
the member records, when it is parsed, whether it is a trait axiom. A library built
without the option keeps its bodiless `{:axiom}` ghost trait members as interface
members, even for a client that uses the option. Without this, a client's option would
silently turn a library's checked interface into an unchecked axiom for the client's
classes.

Not covered:

- **Compiled members.** An unimplemented compiled member has nothing to run, so here
  the issue is ill-posed. The resolver rejects `{:axiom}` on a bodiless compiled trait
  member under the option. The message points to `{:extern}`, the existing way to say
  that the code is elsewhere.
- **Extreme predicates and lemmas.** Their prefix forms are built from the body. They
  are rejected under the option, with a message saying so, until they get a design.
- **Constructors.** Traits have none.

### 5.2 Why it is sound

Relative to the declared axioms, the feature adds nothing:

- **Encoding.** No change. A call `c.foo()` on a `C` that inherits `foo` resolves to
  `T.foo`, as for any inherited member, and uses `T.foo`'s specification. For a ghost
  function, `T.f(this)` stays uninterpreted for dynamic types that do not override it.
  It is constrained only by `T.f`'s own postcondition axiom, which Dafny emits today.
  No override axiom is emitted for a class that does not override.
- **Expressiveness.** The trait author can already write `{:extern} {:axiom}` on a
  bodiless lemma (e4, e8) or ghost function (a9), or give a lemma the body
  `{ assume {:axiom} Post; }` (e3). All are accepted and inherited today. So a program
  accepted under the option proves nothing that a program accepted today cannot.
- **Trust is declared.** `{:axiom}` is required. It is what makes a member an axiom, and
  `dafny audit` already lists it (e13). `{:axiom}` on a member that has a body does not
  skip the body's verification (b12).

The axiom's trust reaches every implementation, including classes in other modules
whose authors did not write it (e16). That is how a library's `{:axiom}` lemma already
works when a client calls it, and the same rule applies: no warning in `verify`, and a
record in `audit`. The plan adds one audit finding: **each class, datatype or newtype
that inherits a trait axiom without overriding it** is listed as relying on it. Then the
module that receives the trust can see it. The audit text for a ghost function axiom
says that the signature is trusted too.

### 5.3 Refinement and diamonds

- A refinement may give a trait axiom a body. For a lemma or method
  (`lemma foo... { proof }`) it is then an ordinary member, verified once in the trait
  for every `this: T`, and classes of the base that override it still may (b14). This is
  how an axiom is later discharged. For a function, a class of the base that overrides
  it is then the existing error (b16), so a function axiom can be given a body only
  where no inherited type overrides it.
- A refinement may not add or remove `{:axiom}` on an inherited bodiless trait member.
  The status is fixed where the member is declared (section 5.1).
- A refinement may strengthen an axiom's specification, as it may that of any bodiless
  member. The stronger statement is a new premise of the refining module. `dafny audit`
  lists it at the refining declaration, as it does today (a10, b15), and a test pins it.
- Diamonds: an axiom reached by two paths is one member, inherited once (b3). Two
  declarations of one name in two parents are the existing clash error (d1).

## 6. Interactions

### 6.1 With issue 211, cross-module termination (https://github.com/erniecohen/dafny/pull/380)

The release used here has `--modular-termination`, so the claims below were run. The
option is marked experimental.

**Issue 88.**

- *Openness.* An added parent from another module must be open: R3 is the existing rule.
  The M4b design already rejects "adding an extensible descendant of a sealed ancestor"
  (`docs/dev/design/211-m4b-library-openness.md`, lines 127-130 and 328). Under issue 88
  that can first arise in a refinement, so the check must run on the merged parents.
- *Bounds of a new override.* Under rule 4 of the 211 plan, an override inherits the
  overridden member's bound, and an explicit bound must be at most every inherited
  bound. That comparison is part of the override check, so in a refining module it is
  assumed today (m1):

  ```dafny
  module Tr {
    trait {:termination false} S {
      function F(other: S, n: nat): int decreases {:global} n
    }
  }
  module A {
    import Tr
    type T {
      function F(other: Tr.S, n: nat): int decreases {:global} n + 1 { other.F(other, n) + 1 }
      lemma Unfold(other: Tr.S, n: nat) decreases {:global} n + 1
        ensures F(other, n) == other.F(other, n) + 1 { }
    }
  }
  module B refines A {
    class T extends Tr.S { constructor () {} }
    method Bad() ensures false decreases {:global} 1 {
      var t := new T();
      var s: Tr.S := t;
      t.Unfold(s, 0);
      assert s.F(s, 0) == t.F(s, 0);
    }
  }
  ```

  With `--modular-termination` this verifies. The one-module control is rejected:
  "function's 'decreases {:global}' bound must be below or equal to that in the trait".
  It is bug 2.1 again, and R4 covers it.
- *Call sites.* A member that becomes an implementation changes what every call
  dispatched to the overridden member can reach. m4 shows it: `A` has a sealed trait
  `S0` with a bodiless `K`, a class `X extends Tr.S` whose `F` calls `s0.K(other, n)`,
  and `type T` whose `K` calls `other.F(other, n)` under a bound. In `A` nothing
  implements `S0.K`, and the call in `X.F` has no obligation. `B` declares
  `class T extends S0`, and `X.F`, `S0.K`, `T.K`, `Tr.S.F` form a cycle. With
  `--modular-termination`, `B` verifies and proves `false`. The control is rejected at
  the call in `X.F`: "cannot prove termination". It is bug 2.2, and R7 covers it.
- *The M4b design already requires this.* "Concrete refinements must check
  changed/inherited implementation obligations against actual bindings. Existing
  inherited-assertion elision, `$_reverifyPost` guards, or reused source origins cannot
  substitute" (`docs/dev/design/211-m4b-library-openness.md`, line 283). m1 and m4 are
  two places where the base does not yet do so. Stage 0 is therefore a prerequisite of
  the 211 work as well, not only of issue 88.
- *Tie.* A cross-module cycle created by a refinement passes through dispatch on `S`,
  whose bounds are fixed in `S`'s module. The argument of the 211 plan, section 2.1,
  holds for `M1.T` on one condition: that `M1` is the module in which `T`'s
  implementations are checked against `S`'s interface, inherited text included.
- *The leaf default* (211 plan, section 2.2). A member of `T` with no bound in `M0`
  makes no dispatched call, or it would have needed a bound there. When it newly
  overrides an `S` member it inherits that member's bound, and its own body gets no new
  obligation. What changes is at call sites, as in the two bullets above. The first
  draft of this plan said only that the member's obligation gets weaker. That missed
  the call sites.

**Issue 89.** A trait axiom has no body, so it makes no call and has no termination
obligation of its own. A dispatched call to it is a call to a leaf: with no declared
bound it gets BOTTOM under the 211 plan's section 2.2, and every caller satisfies that.
A class that overrides it is checked against that bound, as any override. The axiom's
specification is checked for well-formedness, termination of the calls it makes
included, once, in the trait. These claims are argued, not run: the feature does not
exist yet.

### 6.2 With issue 105, member invariants (https://github.com/erniecohen/dafny/pull/384)

Invariants follow `extends` and refinement (105 plan, sections 4.1 and 4.2). Member
invariants are not implemented, so nothing here could be run.

**Issue 88.**

- `M1.T`'s invariant is the conjunction over its ancestors in `M1`, a superset of its
  ancestors in `M0`. `raw M1.T` is below `raw S`, with nothing written (105 plan,
  section 4.1).
- **The 105 plan's refinement rule is the elision of section 2, and needs the same
  repair.** Its section 4.2 assumes inherited conjuncts at inherited sites, because
  "`M0` proved them for this site and this conjunct, and neither has changed". That is
  C3. It also needs C1: a conjunct's text can be captured as in section 2.3, and then
  it is not the conjunct `M0` proved. Under form A the 105 plan's "not re-proved in a
  refinement" (its section 4.4) does not apply until elision returns with its argument.
  Under form B the row of section 3.3 applies, and the "added conjuncts" must be defined
  by declaration, not by origin: they are the conjuncts of ancestors of `M1.T` that are
  not ancestors of `M0.T`, plus the invariants that refining declarations add. `S` may
  be a trait of `M0`, whose conjuncts have inherited origins, made an ancestor of `T`
  only in `M1`.
- Each constructor of a class below `T`, inherited ones included, proves `S`'s conjuncts
  at its `new;`, reported in `M1`. This is the same site as R6's definite-assignment
  obligation. If an inherited constructor cannot, the refinement is wrong, as in 105
  section 4.2.
- An `S` invariant may apply `S`'s raw members to `this`. A member of `T` that newly
  overrides one becomes raw in `M1` and is verified again, without the invariant. This
  is the case 105 section 4.2 already names for added invariants.
- To add `S` as a parent, `M1` must see `S`'s invariant (105 section 4.1, visibility).
  The extension condition of 105 section 3.3 is re-checked in `M1`, as 105 section 4.2
  already requires.
- For value types (stage 5), an added parent with an invariant adds a conjunct at every
  inherited conversion site of the datatype or newtype. Those are new obligations under
  the same rule.

**Issue 89.**

- A trait axiom about members that a class's invariant constrains can contradict that
  invariant. That is trusted, like any axiom.
- A ghost-function axiom used in an invariant is a raw member. Its specification is then
  an axiom about raw values, and the extension condition of 105 section 3.3 applies to it.
  With no body, the condition is checked on its specification, as 105 section 4.3 does
  for bodiless functions of abstract types.
- `this` in a constructor's first division is raw, so a trait axiom about a finished `T`
  cannot be applied to it there.

### 6.3 With each other

With both options, a parent added in a refinement may carry axioms. Classes below `T`
then inherit them, and R5 asks nothing of them for those members. Each feature works
without the other.

## 7. What is checked where

| Check | Where | Feature |
|---|---|---|
| Inherited obligations checked (form A), or assumed only by key (form B) | every site listed at the head of section 2 | stage 0 |
| Inherited names resolve to the copy of their `M0` meaning (form B only) | `RefinementTransformer` cloner and the resolver | stage 0 |
| `... extends` parses only with the option | `Dafny.atg` (class and trait rules) | 88 |
| Parents merged, duplicates dropped | `RefinementTransformer.MergeClass` (`:672`) | 88 |
| Reference-ness unchanged (R1) | resolver, after `SetUpAsReferenceType` (`ModuleDefinition.cs:1087`), comparing with the base | 88 |
| Cycles, cross-module rule, clashes (R2, R3, R4) | existing, `ModuleResolver.cs` | 88 |
| Inherited bodiless members implemented (R5) | existing, `ProcessInheritedTraitMembers` | 88 |
| New override checks, field assignment, termination (R4, R6, R7) | existing checks, made to run by stage 0 | 88 |
| Trait-axiom status recorded per member from the declaring file's options | parser | 89 |
| Trait axioms exempt from R5 | `ProcessInheritedTraitMembers`, next to the `{:extern}` exemption (`:2736`) | 89 |
| `{:axiom}` on bodiless compiled or extreme trait members rejected | same | 89 |
| `{:axiom}` status not changed by a refinement | `RefinementTransformer.MergeClass` | 89 |
| Types that inherit an axiom listed; signature named as trusted | `Auditor` | 89 |
| Bounds and reachability re-checked at inherited sites | the modular-termination checks, with stage 0 | 88 with 211 |
| Added ancestors' invariant conjuncts asserted at inherited `new;` | the 105 implementation | 88 with 105 |
| Library compatibility | `CommonOptionBag.cs`, registration next to `:800-804` | both |

## 8. Options

- **Stage 0** is a soundness fix, so it is on by default, with a hidden opt-out that
  restores the old output byte for byte. Under form A that is one option,
  `--legacy-refinement-elision`. Under form B each of the fixes has its own. The PR
  lists every suite and standard-library verdict that differs between the two settings,
  as `AGENTS.md` requires. Only programs that verified because of the elision may move.
- **`--refinement-extends`** (issue 88), default false. With it off, the grammar and every
  output are unchanged. Registered as library-implies-local.
- **`--inherited-trait-axioms`** (issue 89), default false. With it off, behaviour is
  unchanged. Registered as library-implies-local, as `--allow-axioms` is. In the other
  direction, a client with the option and a library without it, the library's members
  keep the status they were declared with (section 5.1).

Neither feature adds an axiom to Dafny's own encoding, so neither joins
`--additional-axioms`. Each needs both resolvers; neither requires
`--type-system-refresh`. The names are proposals.

## 9. Stages and tests

Each stage is one PR, with a release-note fragment, Reference Manual and `--help`
changes where it changes a documented rule, and tests under
`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/`. Every positive test that
relies on an inherited fact has an `assert false` companion that must fail. Every test
runs under both resolvers.

**Stage 0: refining modules check what they changed** (default on). Tests, each of
which must now fail with the error reported in the refining module:

- override checks: e7, e7c, e14, e15, the generic version (e11), the diamond (e12), the
  library version (e10), the non-reference trait (c4), the abstract import (a7), and a
  chain `M0 <- M1 <- M2` where `M2` strengthens the postcondition;
- termination: a25, a5, a4b; with `--modular-termination`, m1 and m4;
- name capture: a15, a1, a2, a22, a29, and a30, which must not reach run time;
- definite assignment: a3;
- changed body: a26d;
- controls: a refinement that changes nothing still verifies; one that strengthens a
  postcondition in a way the inherited override meets still verifies; the attacks of
  section 2.6 still fail as they do today;
- the suite and standard-library census, option on and off, with resource units.

**Stage 1: issue 89.** Tests:

- the issue's reproducer, accepted; with `ensures false`, `c.foo()` proves `false` only
  with the axiom, and audit lists both the trait and the class;
- a ghost function axiom, inherited; one with an empty result type, with the audit text
  that names the signature (b6);
- generic: `trait T<X>` with an axiom about `X`, inherited by `class C extends T<int>`
  and by a generic `class D<Y> extends T<seq<Y>>`, with the instantiation checked;
- diamond: an axiom reached through two parents, inherited once; two declarations of
  one name, rejected;
- subtraits: an interface member redeclared as an axiom, exempt below the subtrait and
  audited there; an axiom redeclared without `{:axiom}`, which a class below must
  implement;
- refinement chain: an axiom in `M0`, inherited in `M1`, given a body in `M2`, where it
  is then verified; a refinement that adds `{:axiom}`, rejected; a refinement that
  strengthens an axiom, audited at the refining declaration; a function axiom given a
  body while an inherited class overrides it, rejected;
- library: a trait axiom in a `.doo` library, inherited by a client class; a client
  without the option, rejected with the compatibility message; a library built without
  the option whose bodiless `{:axiom}` trait lemma a client class does not implement,
  rejected although the client has the option;
- rejected: a bodiless `{:axiom}` compiled method, an extreme lemma, and option off;
- an override of an axiom is still checked (e5 unchanged).

**Stage 2: issue 88 for traits and classes** (after stage 0). Tests:

- the issue's reproducer, accepted; a variable of type `S` holds an `M1.T`;
- the soundness extremes, each rejected in `M1`: an inherited member of `T` that becomes
  an override of an `S` member with a stronger specification (R4); an inherited class
  below `T` whose member does (R4); a field of `S` of a possibly empty type with an
  inherited constructor (R6); an inherited member whose call through `S` now reaches
  itself (R7); an `S` member that captures a name used in an inherited lemma (R8); a
  base class that does not implement `S`'s members, rejected until it is refined (R5);
- an `S` method with a body, overridden by an inherited member: accepted, with the
  override check; an `S` function with a body, clashing: rejected (R4);
- diamond: `T ... extends S` where `T` and `S` both extend `P`, with `P`'s members
  inherited once; a clash between `S` and `T`, rejected;
- refinement chain: `M1` adds `S`, `M2` adds `R`, a class of `M0` implements both by
  refinement; an abstract import of `M0` instantiated with `M2`, where a class of the
  importing module gets the new obligations;
- generic: `trait T<X> ... extends S<seq<X>>`, with variance and type-parameter errors;
- library: refining a library module and adding a parent; adding an open library trait
  as a parent; adding a sealed library trait, rejected;
- rejected: R1 (a non-reference trait given `object` as an ancestor, under
  `--general-traits=datatype`), a cycle, option off;
- general-trait cardinality: an added parent that closes an expansive cycle, rejected.

**Stage 3: issue 88 with `--modular-termination`** (with the 211 work). Tests: m1 and m4
written with the new syntax, rejected in `M1`; an added sealed parent from another
module, rejected; an inherited leaf that inherits a bound, accepted.

**Stage 4: issue 88 with `--member-invariants`** (after 105 stage 5). Tests: an
inherited constructor that does not establish an added parent's invariant, rejected at
its `new;` in `M1`, where `S` is declared in `M0` (the origin trap of section 6.2); an
ancestor reached by two paths, proved once; a member that becomes raw, re-verified.

**Stage 5: issue 88 for datatypes and newtypes** under `--general-traits`. The same
tests, plus inherited conversion sites under 105.

**Upstream.** Stage 0 is a bug fix in code that this line takes from upstream. None of
section 2 has been checked against upstream `master`. After that is done, stage 0 is a
candidate for an upstream pull request. The two features stay in the fork unless the
owner decides otherwise.

## 10. Open questions for the owner

1. **Stage 0's form.** A: verify refining modules in full (evidently sound, one small
   change, costs resource units and re-reports inherited failures). B: keep the elision
   with exact keys and a no-capture rule (keeps today's cost, soundness rests on the
   list of section 3.3 being complete). Recommendation: A, then measure.
2. **Issues for sections 2.2 to 2.5.** Only 2.1 is filed
   (https://github.com/erniecohen/dafny/issues/404). The others are reproduced and
   have no issue. Under form A they have one cause and one fix, so one issue could
   carry all five, with issue 404 as its first case. Under form B each needs its own.
3. **The 105 and 211 plans** both rely on the elision: 105 in its sections 4.2 and 4.4,
   211 in the M4b requirement that the base does not yet meet (section 6.1). Should
   those plans be amended to depend on stage 0?
4. **Issue 89's scope.** All non-extreme ghost members (recommended, one rule), or
   lemmas only, as in the issue's example.
5. **Option names**: `--refinement-extends`, `--inherited-trait-axioms`,
   `--legacy-refinement-elision`.
6. **Datatypes and newtypes under issue 88** (stage 5): include, or leave the issue at
   classes and traits.
