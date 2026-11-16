# Plan: adding parents in a refinement (issue 88), and inherited trait axioms (issue 89)

Issues: https://github.com/erniecohen/dafny/issues/88 (upstream issue 1471) and
https://github.com/erniecohen/dafny/issues/89 (upstream issue 1466).
Related plans: https://github.com/erniecohen/dafny/pull/380 (issue 211, cross-module
termination) and https://github.com/erniecohen/dafny/pull/384 (issue 105, member invariants).
Base: `review/4.11.0` at `1920c844b`.

This is a plan only. It changes no product file.

## 0. Summary

| | Issue 88: `trait T ... extends S` | Issue 89: unimplemented trait axioms |
|---|---|---|
| Well-posed? | Yes. | Yes for ghost members. Not for compiled members, which have nothing to run; `{:extern}` already covers those. |
| Sound? | Yes, with one condition: every obligation that depends on a type's parents must be checked again where the parents change. **Today Dafny does not do this**, and refinement proves `false` under default options without the new syntax (section 2). | Yes, relative to the declared axioms. It adds no deductive power: Dafny already accepts the same trust spelled `{:extern} {:axiom}`, or as a body `{ assume {:axiom} P; }`. |
| Option | `--refinement-extends`, opt-in | `--inherited-trait-axioms`, opt-in |
| Prerequisite | Stage 0, a default-on soundness fix with its own issue | none |

The two features are independent, apart from one small interaction (section 6.3).

## 1. What Dafny does today

All experiments in this document were run with release `v4.11.0+fcb2042d.review.bcc0c421`
and Z3 5.1.0, with default options and then with `--type-system-refresh`. The two
resolvers agreed in every case. The release is a few commits behind the base, and
none of those commits touch traits, refinement or inheritance.

| # | Program (abridged) | Result |
|---|---|---|
| e1 | the reproducer of issue 88 | parse error `lbrace expected` |
| e2 | the reproducer of issue 89 | `class 'C' does not implement trait lemma 'T.foo'` |
| e3 | trait lemma `foo() ensures false { assume {:axiom} false; }`, class `C extends T` with no `foo` | accepted; `c.foo()` proves `false` |
| e4 | trait lemma `{:extern} foo() ensures false` with no body, class with no `foo` | accepted with a warning that `{:axiom}` silences; proves `false` |
| e5 | trait lemma `{:axiom} foo() ensures false`; class overrides it with `ensures false { }` | override rejected: the trait's `{:axiom}` member is checked today, as an interface |
| e6 | `lemma NotS(c: C) ensures !((c as object) is S)` where `C` does not extend `S` | not provable |
| e7 | abstract type with a member that has a body, refined by a class that adds a parent (section 2) | **proves `false`** |
| e7b | control for e7: the same class written in one module | rejected, as it should be |
| e7c | e7 with a function: `function F(): int { 0 }` overrides `ensures F() > 0` | **proves `false`** |
| e8 | e4 with `{:extern} {:axiom}` | accepted silently; proves `false` |
| e9 | bodiless `ghost function {:axiom}` in a trait, class with no `f` | `class 'C' does not implement trait function 'T.f'` |
| e10, e11, e12 | e7 with the base module from `--library`, with a generic trait, with a diamond | **each proves `false`** |
| e13 | `dafny audit` of a trait with a bodiless `{:axiom}` lemma that classes override | the trait lemma is listed as an axiom |
| e14, e15 | refinement strengthens a trait member's postcondition; an inherited class overrides it (section 2) | **prove `false`** |
| e16 | `{:extern} {:axiom}` lemma in a generic library trait, inherited by a client class at `T<int>` that contradicts it | accepted, proves `false`; `dafny audit` lists the axiom only at the trait |

### 1.1 Issue 88: only the grammar stands in the way

- The grammar offers `extends` **or** `...`, not both: `Dafny.atg:541-543` for classes
  and `Dafny.atg:592-594` for traits. That is the whole parse error of e1.
- The refinement merger already accepts parents on a refining declaration.
  `RefinementTransformer.MergeClass` appends the base's parents to the refining
  declaration's own list (`RefinementTransformer.cs:672`). A refining declaration
  without `...` is rejected earlier (`RefinementTransformer.cs:224-233`).
- Adding parents in a refinement is already possible by one route: an abstract type
  refined by a class or trait that has `extends` (`RefinementTransformer.cs:309-327`).
- The checks that matter run on the merged list after the merge: trait cycles
  (`ModuleResolver.cs:1225-1228`), the cross-module extension rule
  (`ModuleResolver.cs:2398-2418`), name clashes between parents
  (`ModuleResolver.cs:2581`), members that must be implemented and override
  compatibility (`ModuleResolver.cs:2711-2775`), and whether a trait is a reference
  type (`ModuleDefinition.cs:1087`).
- The encoding has only positive facts about parents: one axiom
  `implements$J(class.C, ...)` per declared parent (`BoogieGenerator.Methods.cs:486-502`)
  and `$Is(o, J?) <==> o == null || implements$J(dtype(o))`
  (`BoogieGenerator.Methods.cs:131-137`). Nothing says a type is *not* below a trait,
  and e6 confirms that no module can prove it. So adding a parent never falsifies a fact
  that was provable before. Section 4.3 builds on this.

### 1.2 Issue 89: two exemptions already exist

`ProcessInheritedTraitMembers` (`ModuleResolver.cs:2711-2746`) requires every class to
redeclare each bodiless trait member, with two exceptions:

- `{:extern}` members (`ModuleResolver.cs:2736-2738`). With `{:axiom}` added, the
  warning of e4 goes away. That is issue 89's semantics today, spelled with an
  attribute that means something else.
- reveal lemmas (`ModuleResolver.cs:2739-2741`). The fork's comment there states the
  hazard: "an unimplemented one would let a call assume its postcondition".

`dafny audit` already reports a trait's bodiless `{:axiom}` lemma as an axiom
(`Auditor/Assumption.cs:14`), even though e5 shows that today it is checked, not
assumed. Under issue 89 the report becomes accurate.

## 2. A soundness bug in refinement, found on the way

**Override checks for inherited members are skipped in refining modules, even when the
overridden specification changed.**

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

This verifies under default options (e14), with no axiom and no attribute.
`dafny audit` reports nothing. The function version (e15) and the abstract-type route
(e7) are just as bad:

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
`S extends P` (e12), and with `A` loaded through `--library` (e10). In one module, the
control (e7b) is rejected as it should be: "the method must provide an equal or more
detailed postcondition than in its parent trait".

**The cause.** The override check asserts each parent postcondition at the
*overriding* member's origin: `Assert(m.Origin, ...)` (`BoogieGenerator.Methods.cs:1561`,
and `:1592`, `:1190`, `:1290` for requires and functions). In `B`, `C.L` is a copy whose
origin is a `RefinementOrigin`, so `IsInherited(B)` holds (`RefinementOrigin.cs:22-24`).
`Assert` then emits an assume (`BoogieGenerator.cs:3712-3716`). With no asserts left,
the override check has no implementation at all (`EmitImplementation`,
`BoogieGenerator.cs:1729-1736`). The Boogie output of e7 shows
`procedure OverrideCheck$$B.T.L` with no `implementation`. The escape hatch
`MustReverify` (`MethodOrConstructor.cs:34`) is never set to true anywhere in the code.

The elision rests on an assumption that does not hold: "an inherited site was proved in
the refined module". That is true of a statement in an inherited body. It is false of an
override check, whose statement depends on the parent's specification and on which
members the type overrides, and refinement can change both.

**This is a default-on soundness fix (stage 0).** It needs its own issue and PR, before
issue 88, which would otherwise widen the hole. Section 3 states the rule it implements.
The precedent for the fix is already in the code: `BoogieGenerator.Decreases.cs:114-117`
wraps an inherited call site in `ForceCheckOrigin` when the context it is checked
against is new.

## 3. The principle: what an inherited obligation may assume

Let `M1 refine M0`. Dafny translates `M1` as a whole and turns inherited obligations into
assumptions. That is sound for an obligation only if **the same proposition, under the
same hypotheses, was proved in `M0`**. A source origin identifies the proposition only
when the proposition is determined by the inherited text at that site.

Three kinds of obligation also depend on the ancestry of the enclosing type, which a
refinement can change:

1. **Override checks**: which member is overridden, and its specification.
2. **Invariant checks at `new;`** (issue 105): which conjuncts the type has.
3. **Termination bounds** (issue 211): the bound a member inherits.

For these, the obligation key is not the origin but the pair *(site, the inherited
declaration it is checked against)*. An obligation is elided only if that pair existed
in `M0` and neither element changed. Otherwise it is asserted, with an error reported in
`M1` at the inherited site. Stage 0 implements this for kind 1. Issues 105 and 211 must
use the same rule for kinds 2 and 3 (sections 6.1 and 6.2).

The simplest correct form of stage 0 asserts every override check in a refining module
and elides none. It costs one override check per inherited overriding member per
refinement. Stage 0 measures that cost on the suite and the standard library, in
resource units. If it matters, the exact rule above replaces it.

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
added ones (`MergeClass` already does this). A parent named twice counts once.

`M1.T` is a new type, distinct from `M0.T`. The feature never changes `M0`. It says
that the type `M1` builds from `M0.T` also lies below `S`.

Scope: classes and traits. Datatypes and newtypes may extend traits under
`--general-traits`, and the same rules apply to them. They come in a later stage
(stage 5), because an added parent with an invariant puts obligations on every inherited
conversion site of a value type (section 6.2).

### 4.2 Rules

An added parent is checked as if it had been written in `M1` on a fresh declaration,
plus these rules:

- **R1. Reference-ness is fixed.** A refinement may not turn a non-reference trait into a
  reference trait. Under `--general-traits` a trait is a reference type iff it extends
  `object` (`ModuleDefinition.cs:1087`). A datatype in `M0` may extend a non-reference
  `T`, and code in `M0` may rely on `T` not being a reference type, for example in
  equality support. The error names the added parent. (Under the legacy setting every
  trait is a reference type, so R1 never fires.)
- **R2. Cycles** are the existing check (`ModuleResolver.cs:1225-1228`) on the merged
  parents.
- **R3. Cross-module parents.** The existing rule (`ModuleResolver.cs:2398-2418`) applies
  to each added parent, with `T`'s module being `M1`. A parent from another module must
  be open (`{:termination false}`), or, without `--modular-termination`, `T` must carry
  `@AssumeCrossModuleTermination`. Both are reported by the default-on warning
  (`ModuleResolver.cs:1102-1135`).
- **R4. Members.** Name clashes between parents are the existing error
  (`ModuleResolver.cs:2581`). A member of `T` that already existed in `M0` and has the
  name of a member of `S` becomes an override of it. That override is a **new
  obligation** of `M1` (section 3, kind 1), checked by the existing override check,
  which stage 0 makes run. A field of `S` clashing with a member of `T`, or a fully
  defined member of `S` clashing with a member of `T`, is the existing error
  (`ModuleResolver.cs:2773`).
- **R5. Classes from the base.** A class of `M0` that extends `T` now extends `S`. It
  must implement `S`'s bodiless members, by the existing check
  (`ModuleResolver.cs:2742-2743`). To do so it is refined in `M1` (`class C ... { ... }`),
  which can already add members. Under issue 89 an `S` axiom needs no implementation.
- **R6. Type parameters.** The added parent's type arguments may use `T`'s type
  parameters. Variance and characteristic checks are the existing ones, run in `M1`.
- **R7. Cardinality admission** for general traits (`docs/dev/GeneralTraitCardinality.md`)
  runs on the merged parents. An added parent adds an inclusion edge, which that
  analysis checks with the others. A test confirms this.

### 4.3 Why it is sound

Let `Γ0` be the axioms Dafny emits for `M0`, and `Γ1` those for `M1`. Let `σ` map each
declaration of `M0` to its copy in `M1`.

1. **Nothing proved in `M0` becomes false.** `Γ1` contains `σ(Γ0)`: adding a parent
   removes and weakens no axiom. It adds positive `implements$S` facts and the axioms of
   `S`'s members. The encoding has no negative parent fact (section 1.1, e6). So every
   valid verification condition of `M0` stays valid, after `σ`, in `M1`. That justifies
   assuming `M0`'s proofs of obligations whose statement did not change.
2. **Everything new in `Γ1` is proved in `M1`.** The new facts are that `M1.T`'s values
   are `S` values. For that to be consistent, `T`'s values must meet `S`'s contracts:
   - every override of an `S` member by a member of `T` or of a class below `T` meets
     `S`'s specification (R4, override checks, stage 0);
   - every class below `T` implements `S`'s bodiless members (R5), or inherits an
     axiom (issue 89, an explicit trusted premise);
   - every object below `T` satisfies `S`'s invariants (issue 105, section 6.2);
   - every dispatched call through `S` terminates (issue 211, section 6.1).
3. **Reference-ness and characteristics are unchanged (R1),** so no type-level fact of
   `M0`, such as equality support or auto-initialization, is lost.

Point 2 is where the existing elision fails (section 2). Issue 88 is sound only after
stage 0.

### 4.4 Refinement chains, abstract imports, libraries

- **Chains.** In `M0 <- M1 <- M2`, each refinement may add parents. `M2` checks the
  parents added since `M1`, against `M1`'s proofs. Section 3 applies at each step.
- **Abstract imports.** A module verified against `import X : M0` and later instantiated
  with `M1` used only what `M0` says. By point 1 that still holds of `M1`.
- **Exports.** Clients see `S` among `T`'s parents exactly when `M1` reveals `T`.
- **Libraries.** A library module may be refined and given parents. The refined module
  is verified in the client, and stage 0 makes the override checks run there. A library
  trait may be an added parent under R3. A `.doo` library whose source uses the syntax
  needs the option in the client. The option is registered as
  `OptionCompatibility.OptionLibraryImpliesLocalError`, as `--allow-axioms` is
  (`CommonOptionBag.cs:803`).

## 5. Issue 89: trait axioms are inherited

### 5.1 Meaning

Under `--inherited-trait-axioms`, a trait member that

- has no body,
- is marked `{:axiom}`, and
- is a ghost member that is not extreme: a lemma, a two-state lemma, a ghost method, a
  ghost function or predicate, or a two-state function or predicate,

is a **trait axiom**. A class, datatype or newtype that extends the trait and does not
declare the member inherits it, as it inherits a member with a body. It may still
override it, and then the override is checked as today (e5).

A trait axiom states, as a trusted premise, its specification for every value of the
trait: for a lemma, `forall this: T, x :: Pre(this, x) ==> Post(this, x)`, with the heap
where it applies. That is the meaning callers through `T` already assume today. The
feature changes only who is asked to prove it: nobody, rather than each implementation.

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
- **Expressiveness.** For lemmas, the trait author can already write a body
  `{ assume {:axiom} Post; }` (e3), or `{:extern} {:axiom}` (e4). Both are accepted and
  inherited today. So a program accepted under the option proves nothing that a program
  accepted today with `--allow-axioms`-style annotations cannot.
- **Trust is declared.** `{:axiom}` is required. It is what makes a member an axiom, and
  `dafny audit` already lists it (e13).

The axiom's trust reaches every implementation, including classes in other modules
whose authors did not write it (e16). That is how a library's `{:axiom}` lemma already
works when a client calls it, and the same rule applies: no warning in `verify`, and a
record in `audit`. The plan adds one audit finding: **each class, datatype or newtype
that inherits a trait axiom without overriding it** is listed as relying on it. Then the
module that receives the trust can see it.

### 5.3 Refinement and diamonds

- A refinement may give a trait axiom a body (`lemma foo... { proof }`). It is then an
  ordinary lemma, verified once in the trait, for every `this: T`. This is how an axiom
  is later discharged.
- A refinement may not add or remove `{:axiom}` on an inherited bodiless trait member.
  The member's status, interface or axiom, is fixed where it is declared, so that every
  module that sees the trait reads it the same way.
- Diamonds: an axiom reached by two paths is one member, inherited once. Two different
  members of the same name are the existing clash error (`ModuleResolver.cs:2581`).

## 6. Interactions

### 6.1 With issue 211, cross-module termination (https://github.com/erniecohen/dafny/pull/380)

**Issue 88.**

- *Openness.* An added parent from another module must be open: R3 is the existing rule.
  The M4b design already rejects "adding an extensible descendant of a sealed ancestor"
  (`docs/dev/design/211-m4b-library-openness.md`, lines 127-130 and 328). Under issue 88
  that can first arise in a refinement, so the check must run on the merged parents.
- *Bounds.* Under rule 4 of the 211 plan, an override inherits the overridden member's
  bound, and an explicit bound must be at most every inherited bound. When `M1` adds `S`,
  a member of `T` that newly overrides an `S` member inherits `S`'s bound. Two things
  follow:
  - The override termination check (`AddOverrideTerminationChk`, `BoogieGenerator.Methods.cs:925`, part of the override
    check) is a new obligation: kind 1 of section 3, covered by stage 0.
  - If a member's *effective* bound changes, because it had none in `M0` and now inherits
    one, every termination obligation in its inherited body, and in the bodies of the
    implementations below it, is checked against the new bound. That is kind 3 of
    section 3. It is implemented as `BoogieGenerator.Decreases.cs:114-117` already does
    for a new context: the inherited site gets a `ForceCheckOrigin`.
- *Tie.* A cross-module cycle created by a refinement passes through dispatch on `S`, whose
  bounds are fixed in `S`'s module. The argument of the 211 plan, section 2.1, is
  unchanged: each implementation is checked in its own module against `S`'s interface.
- *The leaf default* (211 plan section 2.2). A member of `T` that had BOTTOM in `M0`
  and inherits a proper bound from `S` in `M1` gets a weaker obligation, not a stronger
  one. Its body is still checked in `M1`, by the previous bullet.

**Issue 89.** A trait axiom has no body, so it makes no call and has no termination
obligation of its own. A dispatched call to it is a call to a leaf: with no declared
bound it gets BOTTOM under the 211 plan's section 2.2, and every caller satisfies that.
A class that overrides it is checked against that bound, as any override. The axiom's
specification is checked for well-formedness, termination of the calls it makes
included, once, in the trait.

### 6.2 With issue 105, member invariants (https://github.com/erniecohen/dafny/pull/384)

Invariants follow `extends` and refinement (105 plan, sections 4.1 and 4.2).

**Issue 88.**

- `M1.T`'s invariant is the conjunction over its ancestors in `M1`, a superset of its
  ancestors in `M0`. `raw M1.T` is below `raw S`, with nothing written (105 plan,
  section 4.1).
- **The "added conjuncts" of 105 section 4.2 must be defined by declaration, not by
  origin.** They are the conjuncts of ancestors of `M1.T` that are not ancestors of
  `M0.T`, plus the invariants that refining declarations add. `S` may be a trait of `M0`,
  whose conjuncts have inherited origins, made an ancestor of `T` only in `M1`. If
  added conjuncts were recognised by their origin, the check at an inherited `new;`
  would be elided, which is section 2's bug in another place. This is kind 2 of
  section 3. 105 section 4.4 already keys conjuncts by declaring type, so the change is
  in what counts as "inherited", not in the encoding.
- Each constructor of a class below `T`, inherited ones included, proves `S`'s conjuncts
  at its `new;`, reported in `M1`. If an inherited constructor cannot, the refinement is
  wrong, as in 105 section 4.2.
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
| `... extends` parses only with the option | `Dafny.atg` (class and trait rules) | 88 |
| Parents merged, duplicates dropped | `RefinementTransformer.MergeClass` (`:672`) | 88 |
| Reference-ness unchanged (R1) | resolver, after `SetUpAsReferenceType` (`ModuleDefinition.cs:1087`), comparing with the base | 88 |
| Cycles, cross-module rule, clashes (R2-R4) | existing, `ModuleResolver.cs` | 88 |
| Inherited bodiless members implemented (R5) | existing, `ProcessInheritedTraitMembers` | 88 |
| Trait axioms exempt from R5 | `ProcessInheritedTraitMembers`, next to the `{:extern}` exemption (`:2736`) | 89 |
| `{:axiom}` on bodiless compiled or extreme trait members rejected | same | 89 |
| `{:axiom}` status not changed by a refinement | `RefinementTransformer.MergeClass` | 89 |
| Override checks asserted in refinements | `BoogieGenerator.Methods.cs`, override-check builders | stage 0 |
| Termination bounds re-checked where an effective bound changes | `BoogieGenerator.Decreases.cs` and the modular-termination checks | 88 with 211 |
| Added ancestors' invariant conjuncts asserted at inherited `new;` | the 105 implementation | 88 with 105 |
| Classes that inherit an axiom listed | `Auditor` | 89 |
| Library compatibility | `CommonOptionBag.cs`, registration next to `:800-804` | both |

## 8. Options

- **Stage 0** is a soundness fix, so it is on by default. Its hidden opt-out is
  `--legacy-elided-override-checks`, which restores the old output byte for byte. Its PR
  lists every suite and standard-library verdict that differs between the two, as
  `AGENTS.md` requires. Only programs that verified because of the elision may move.
- **`--refinement-extends`** (issue 88), default false. With it off, the grammar and every
  output are unchanged. Registered as library-implies-local.
- **`--inherited-trait-axioms`** (issue 89), default false. With it off, behaviour is
  unchanged. Registered as library-implies-local, as `--allow-axioms` is.

Neither feature adds an axiom to Dafny's own encoding, so neither joins
`--additional-axioms`. Each needs both resolvers; neither requires
`--type-system-refresh`. The names are proposals.

## 9. Stages and tests

Each stage is one PR, with a release-note fragment, Reference Manual and `--help`
changes where it changes a documented rule, and tests under
`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/`. Every positive test that
relies on an inherited fact has an `assert false` companion that must fail. Every test
runs under both resolvers.

**Stage 0: the override-check fix** (its own issue, default on). Tests:

- e7, e7c, e14, e15: each must now fail, at the inherited override, with the error
  reported in the refining module.
- extremes: the generic version (e11), the diamond (e12), the library version (e10), and a
  chain `M0 <- M1 <- M2` where `M2` strengthens the postcondition. Each must fail.
- controls: a refinement that changes nothing still verifies; one that strengthens the
  postcondition in a way the override meets still verifies.
- the suite and standard-library census, option on and off, with resource units for the
  added checks.

**Stage 1: issue 89.** Tests:

- the issue's reproducer, accepted; with `ensures false`, `c.foo()` proves `false` only
  with the axiom, and audit lists both the trait and the class;
- generic: `trait T<X>` with an axiom about `X`, inherited by `class C extends T<int>`
  and by a generic `class D<Y> extends T<seq<Y>>`, with the instantiation checked;
- diamond: an axiom reached through two parents, inherited once; two different axioms of
  one name, rejected;
- refinement chain: an axiom in `M0`, inherited in `M1`, given a body in `M2`, where it
  is then verified; a refinement that adds `{:axiom}`, rejected;
- library: a trait axiom in a `.doo` library, inherited by a client class; a client
  without the option, rejected with the compatibility message;
- rejected: a bodiless `{:axiom}` compiled method, an extreme lemma, and option off;
- an override of an axiom is still checked (e5 unchanged).

**Stage 2: issue 88 for traits and classes** (after stage 0). Tests:

- the issue's reproducer, accepted; a variable of type `S` holds an `M1.T`;
- the soundness extremes: an existing member of `T` that becomes an override of an `S`
  member with a stronger specification must fail in `M1` (stage 0's check, reached by
  the new syntax); a base class that does not implement `S`'s members, rejected until
  it is refined;
- diamond: `T ... extends S` where `T` and `S` both extend `P`, with `P`'s members
  inherited once; a clash between `S` and `T`, rejected;
- refinement chain: `M1` adds `S`, `M2` adds `R`, a class of `M0` implements both by
  refinement; an abstract import of `M0` instantiated with `M2`;
- generic: `trait T<X> ... extends S<seq<X>>`, with variance and type-parameter errors;
- library: refining a library module and adding a parent; adding an open library trait
  as a parent; adding a sealed library trait, rejected;
- rejected: R1 (a non-reference trait given `object` as an ancestor, under
  `--general-traits=datatype`), a cycle, option off;
- general-trait cardinality: an added parent that closes an expansive cycle, rejected.

**Stage 3: issue 88 with `--modular-termination`** (with the 211 work). Tests: an
inherited member that inherits a bound from an added parent and whose body violates it,
rejected in `M1`; the Tie program built through an added parent, rejected; the leaf
default unchanged.

**Stage 4: issue 88 with `--member-invariants`** (after 105 stage 5). Tests: an
inherited constructor that does not establish an added parent's invariant, rejected at
its `new;` in `M1`, where `S` is declared in `M0` (the origin trap of section 6.2); an
ancestor reached by two paths, proved once; a member that becomes raw, re-verified.

**Stage 5: issue 88 for datatypes and newtypes** under `--general-traits`. The same
tests, plus inherited conversion sites under 105.

**Upstream.** Stage 0 is a bug fix in code that this line takes from upstream. After it
is confirmed on upstream `master`, it is a candidate for an upstream pull request. The
two features stay in the fork unless the owner decides otherwise.

## 10. Open questions for the owner

1. **Stage 0's form.** Assert every override check in a refining module (simple, costs
   some resource units), or elide by the exact rule of section 3. Recommendation: the
   simple form first, then measure.
2. **Issue 89's scope.** All non-extreme ghost members (recommended, one rule), or
   lemmas only, as in the issue's example.
3. **Option names**: `--refinement-extends`, `--inherited-trait-axioms`,
   `--legacy-elided-override-checks`.
4. **Datatypes and newtypes under issue 88** (stage 5): include, or leave the issue at
   classes and traits.
