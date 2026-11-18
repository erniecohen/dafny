# Member invariants: a guide to the commits

This is the detailed reading guide to https://github.com/erniecohen/dafny/pull/419, which implements member invariants (https://github.com/erniecohen/dafny/issues/105) by the plan in [`issue105-member-invariants-plan.md`](issue105-member-invariants-plan.md). The pull request's description is a summary; this file has what each commit does, step by step, the option-off comparison of each step, and the tests each step added. The decisions taken while implementing are in section 10 of the plan, under their steps, and so are the tables of controls.

## The commits

Merges of `review/4.11.0` and the "Restamp the product commit as …" commits (which keep `.github/review/base` naming the latest product commit) are mechanical and can be skipped. In order:

**The plan.** `44b61f29b` through `9c15abc29`, then `fb46506dc` and `5dd33ee1c`: `docs/dev/issue105-member-invariants-plan.md`, from https://github.com/erniecohen/dafny/pull/384, amended to the codatatype declaration rule (section 3.8). Section 10 records each decision made while implementing, step by step.

**Step 1: option, syntax and resolution.** `d0ca3d758`, review fixes `d20ccd0e4`.
- The hidden option, checked on the command line and again by the resolver. A `.doo` library built with it requires it of its clients.
- `invariant e` members of a datatype, class, trait, newtype or abstract type, parsed, printed, cloned and resolved as anonymous ghost predicates (`invariant#k`). They are never opaque, never override and are never overridden; refinement numbers new invariants after the inherited ones.
- The reads restriction of section 3.2: an invariant depends on no mutable state.
- The codatatype ban, and the declaration rule of section 3.8: a program that declares an invariant declares no codatatype.
- `witness e` and `raw T` parsed, and rejected as not yet supported.

**Step 2: `raw T` for datatypes.** `4c100f164`, then `6df61ae00` (tests after merging step 1) and `36ae63b39` (the translation cache steps aside, narrowed in step 3).
- A datatype `D` with invariants is itself `raw D`. `D` in a type position denotes a derived declaration, `InvariantTypeDecl`, a subset type of `raw D` whose constraint is the conjunction of the invariants. Constructors, discriminators, static members, imports and export sets refer to the datatype as before.
- Constructor applications and updates have type `raw D`; `D` converts to `raw D` freely, and `raw D` to `D` with an obligation.
- Raw members are inferred in type refinement's fixpoint, from `D`'s own invariants and raw members only.
- A datatype provided but not revealed is an abstract type to its clients, so `raw D` there is not yet supported (step 7 leaves it so; see section 10 of the plan).
- The not-yet-supported gate becomes program-wide.

**Step 3: verification of non-generic datatypes without parents.** `64ee1365a`, then `e50d3c2c2`.
- The gate is lifted for an inductive datatype with no type parameters and no parent traits, in a module that refines no other.
- Encoding (section 5.1), from existing machinery with no prelude change: `$Is(v, Tclass.D) <==> $Is(v, Tclass.D#raw) ∧ D.invariant#0(v) ∧ …`. The raw class constant is `class.D#raw`.
- Conversions to `D` check each invariant separately ("invariant might not hold", the new `InvariantCheck` obligation, with the invariant as related location). A member that is not raw, applied to a `raw D`, checks its receiver.
- Witnesses (section 3.6): `witness e` makes `D` auto-initializable, `ghost witness e` nonempty, and without one `D` is possibly empty. A compiled witness compiles the derived declaration as a subset type `D#` whose default value is the witness.
- The extension condition of section 3.3, all five clauses in conservative form, in `InvariantExtensionCondition`, run after the reads restriction. The hidden `--unchecked-invariant-extension` turns it off, only for the vacuity controls.
- `AnalyzeTypeConstraints` analyses a declaration's base type first.
- `e50d3c2c2`: the translation cache steps aside only while the gate is active. Otherwise a module's translation depends only on what its key covers.

**Step 4: emptiness, the raw-typing audit, generic datatypes.** `dbf5e7dcb`.
- The gate is lifted for generic datatypes, witnesses included. It now keeps out datatypes with parent traits, datatypes in refining modules, and the other kinds of type.
- Encoding: `D<X>` is the subset type of `raw D<X>` whose constraint is the invariants instantiated with `X`. `Tclass.D(X)` and `Tclass.D#raw(X)` take the same type descriptors, so the translator needed no change beyond the gate.
- Variance: this step made the derived declaration's type parameters non-variant, whatever `D` declared. Step 6a replaces that rule with variance by polarity (see section 10 of the plan).
- Witnesses: the witness member is `static const witness#k: D<X, ...>`, and the derived declaration's witness is `D<Y, ...>.witness#k`, so it is checked for every instantiation. A compiled witness compiles as the generic subset type `D#<X, ...>`.
- The extension condition analyses a generic type with invariants that the cone reaches as a generic subset type: its invariants as its constraint, with each type parameter dependent where its argument is.
- Emptiness, audited with no change needed: every consumer (definite assignment, havoc, such-that, new arrays, `(0)` and `(00)`, class-field constructors, CanCall trimming) asks `Type.GetAutoInit`, whose subset-type branch reads the witness kind that the derived declaration has. A datatype with invariants behaves exactly as a subset type with the corresponding witness clause.

**Step 5: classes.** `a5a6e3816`.
- The gate is lifted for classes that declare no parent trait. A class that extends a trait stays behind it until step 6, with traits, newtypes and abstract types.
- Types: the class declaration is the raw type `raw C?`. `C?` names a derived declaration whose constraint is `v != null ==> ` the invariants, with the class's parents (`object?` and its traits). `C` is the non-null type of `C?` and a subtype of `raw C`, the new `RawNonNullTypeDecl`, which is a subtype of nothing else. Boogie names them `C?`, `C`, `C#raw?` and `C#raw`; an object's dynamic type is its class.
- The check at `new;`: until `new;`, explicit or implicit, the new object is only a `raw C`. There each invariant is asserted, and from then on `this` is a `C`. Stored fields are members of `raw C`, so the first division needs no raw `this`.
- Constructors: a class that declares or inherits an invariant declares a constructor. A bodiless constructor assumes the invariants of the object it returns, so `dafny audit` reports it.
- Raw references reach `object`, a trait, the covariant elements and type arguments of collections and other types, and arrow results only after a check that the reference is a `C` or `C?` (`InvariantTypeDecl.FinishRawReferences`). Arrow parameter positions get the membership check alone. Frames carry no obligation.
- Type tests: `o is C` from a reference that is not raw tests the dynamic type and compiles; a test of a raw reference evaluates the invariants and is ghost.
- Export sets, also a fix for datatypes: an invariant is revealed wherever its type is, and nowhere else (see section 10 of the plan).
- Compilation: all four types compile to the class. The Go back end treats the class as the subtype of `object` it is in the target.
- The translation cache no longer steps aside for a class whose invariants are verified.

**Step 6: traits.** `8724a7c6e`.
- The gate is lifted for traits, reference and general, and for the datatypes and classes that extend them, in a module that refines no other.
- Types: a type that declares no invariant but inherits some gets the derived declarations that one with its own invariants gets. Whether it inherits any is known only from its resolved extends clauses, so `CreateInheritedInvariantTypeDecls` resolves clones of them first, into a sink, and makes the derived declarations to a fixpoint. A reference trait is treated as a class is (`raw P?`, `P?`, `P`, `raw P`), a general trait as a datatype is (`raw G`, `G`).
- Constraint: the inherited invariants come first, in a post-order walk of the extends clauses that visits each trait once, as `(v as raw P<args>).invariant#k()` at the parent's type arguments, guarded by `v != null` for a reference type; then the type's own.
- Subtyping: `raw C` is a subtype of `raw P` for each parent trait `P` with invariants. A raw value reaches `object`, or a trait without invariants, only as a finished value of its own type. The Boogie parent axiom relates the raw child to the raw parent, and the variance check of an extends clause uses the raw parent.
- Type tests, joins and receivers: see section 10 of the plan.
- Well-formedness, also a fix for datatypes: each invariant is well formed where the invariants before it hold (the inherited ones, then its type's own earlier ones), as successive requires clauses are. Before, a datatype's second invariant could not rely on its first.
- The extension condition runs over the inherited invariants too, with each trait's type parameters mapped through the extends clauses.
- Witnesses: a datatype that only inherits invariants may declare a witness, checked against them as well as its own. A datatype with no invariant at all may not, even if it extends traits, and neither may a trait yet.
- Two fixes to earlier steps: the parser accepts `invariant` or `witness` right after a method or function with no body, and the printer names a general trait's derived type without `?`.
- The translation cache no longer steps aside for a trait whose invariants are verified, nor for a class that extends one.

**Step 6a: variance by polarity, controls, the IDE.** `9fb7346e7`, `46a2d9ba0`, the verdicts in `cdd61bc6b`, and `5ed9e500c`.
- `9fb7346e7`, variance by polarity (section 3.5, and "Variance by polarity" in section 10). `D<X>` keeps the variance `D` declares, and the resolver checks that each invariant is monotone in a covariant type parameter and antitone in a contravariant one; otherwise it reports an error at the type parameter, naming the offending occurrence as a related location. `TypeParameterPolarity`, in the resolver, computes the polarity of type parameters in formulas, with least-fixpoint summaries of functions and constants, independently of invariants, so that the fix for https://github.com/erniecohen/dafny/issues/491 can apply it to the constraint of a subset type or newtype. Joins of `D<A>` and `D<B>` use the declared variance. New test `statements`: statements inside expressions are not analysed by the extension condition or the polarity check.
- `46a2d9ba0`, controls. Every addition (axiom, assumption) has a positive control and every check a negative one, confirmed by a local mutation run that disabled each site in turn (see "Controls" below). Four things that no test could catch were removed: the assumption after the checks of a conversion to `D` that the value is in `D` (the introduction axiom derives it), the parser's unreachable error for `raw` before something that is not a named type, the reads restriction's own errors for two-state constructs (the existing checks of a function body reject them first), and the error for extending a trait whose invariants are not revealed (the existing rules reject it first; the resolver now asserts it). `docs/dev/obligation-producers.json` follows.
- `46a2d9ba0`, the IDE. `MemberInvariantsTest` in `DafnyLanguageServer.Test` sets the option in a project file and checks diagnostics, related locations, verification statuses, hovers and document symbols. It found one fault, fixed here: the hover and the verification tree showed the reserved names `invariant#k` and `witness#k`. An invariant is now named "invariant of `D`" and described as "invariant <expression>", a witness "witness of `D`" and "witness <expression>". `review.yml` runs the test in a step of its own, as it runs the other editor tests it selects.
- `cdd61bc6b`, on its own: the expected verdicts of `generics`, `classes` and the new `statements`, with the reason each moved.
- `5ed9e500c`: `MemberInvariantsTest.VerificationOnSave` (verify on save, with the option in `dfyconfig.toml`). Section 10 now argues the case of a lemma whose postcondition mentions the type's membership, called inside an invariant, and names that decision as the first target of the adversarial review at the end. It also explains incremental reuse in the IDE: the language server uses the same translation cache, which steps aside only while the gate is active, when nothing is translated, and goes with the gate at the last step.

**Step 7: refinement and abstract types.** `dfa133450`, the verdicts in `4b56eb1ad`, the merge `c42367c0f`, and the mutation results in `0a6336ac4`.
- The gate is lifted for refining modules and for abstract types: the invariants of datatypes, classes, traits and abstract types, declared or inherited, are verified in any module. Only newtypes stay behind it.
- Inherited sites (section 4.2, `BoogieGenerator.InheritedInvariants.cs`). A refining module can add invariants to a type of its base: in the refining declaration, in a trait the type extends, or with a new parent. A copied assert is an assume, since the base proved it, but for a conversion to the refined type that holds only for the conjuncts the base checked at that site. So at an inherited site each conjunct is assumed if the base's constraint has the invariant with the same number from the same lineage, and is checked otherwise, with the refining module as a related location. Invariant numbers are stable under refinement, so an inherited conjunct costs the refining module nothing, as section 4.2 asks. A membership check that is not split into conjuncts is checked whole if any of them is new.
- What is verified again: of a copied member, only an implementation that makes such a check or belongs to a member that became raw (`IsReverifiedInheritedCheck`); its other tasks assert nothing new and are dropped. A refining module that adds no invariant verifies no inherited code again. A copy whose base is a library enters the verification scope when its module must check it.
- Members that became raw: an added invariant may make an inherited member raw (section 3.4). Its receiver no longer satisfies the invariants the base's proof may have used, so its body is verified again, inherited postconditions included.
- A fix to an earlier step, for soundness: the by-method body of a raw function was verified with a receiver that satisfied the invariants, though it runs on raw receivers, so a program could prove that a call returned 2 while it printed 1. The receiver of a by-method body, and of a prefix predicate or lemma, now has the rawness of the member it belongs to (`ModuleResolver.GetReceiverType`). `datatypes` has the controls.
- The check at an implicit `new;` of an inherited constructor is marked as inherited, as an explicit one is.
- Abstract types (section 4.3): an abstract type may declare invariants. Its name denotes the raw type restricted by them, and `raw T` the abstract type itself. The rules the plan left open are in section 10 of the plan.
- `raw` and export sets: the view an export set makes of a declaration that is provided only is marked (`ProvidedOnlyViewOf`), so `raw` of it is rejected with a message that names the original kind ("'raw' is not yet supported for class 'Cell', which is provided but not revealed here").
- The translation cache steps aside only for newtypes.
- `4b56eb1ad`, on its own: the expected verdicts of `datatypes` and `trait-gates` and of the new `refinement-verify`, `abstract-types` and `abstract-synonym`, with the reason each moved.
- `c42367c0f` merges `review/4.11.0`, which brings the fix for https://github.com/erniecohen/dafny/issues/415.
- `0a6336ac4`, the plan only: the table of sites with the results of a mutation run of each site against the whole suite (see "Controls"). The runs of steps 6 and 6a covered only the tests predicted for each site, so their lists were incomplete.

**Step 8: newtypes.** `9f6253ad4`, `d22e4578c`, the fix `ff1f25695`, the verdicts in `87ca94c53`, the controls in `124176d7f` with their verdict in `01babcb9c`, and the mutation results in `dd7eaa6e9`.
- `9f6253ad4`, a fix without the option: a conversion from a newtype to a trait that it extends and that is not a reference type failed an internal assertion of the verifier ("No translation implemented from int to G"), since `ConvertExpression` looked at the newtype's base type, which does not show the trait. The value is now boxed, as a datatype's is. Only conversions that failed before take the new branch. `newtype-trait-cast` is the test.
- `d22e4578c`: the invariants of newtypes, declared or inherited from a trait, are verified. Newtypes were the last kind the gate held back, so the gate (`DeclaresUnsupportedMemberInvariants` and its errors) and the translation cache's step-aside are gone. The resolver's early rejections stay as controls (`not-yet`).
- Types: as for the other kinds, a newtype `N` with invariants is `raw N`, the newtype declaration, and its derived declaration, the subset of `raw N` that satisfies the invariants, which the name `N` denotes.
- The old syntax (section 3.5): where a newtype with invariants also has a constraint or a witness clause in its header, the derived declaration takes both before they are resolved (`NewtypeDecl.TakeHeaderClauses`), and `raw N` is the base type with no constraint. The header's constraint is one more conjunct of `N`, first, as `var x: B := this as B; P(x)`, with the old syntax's message and well-formedness check. The printer, the cloner and a refinement see the header as written. A header variable of inferred type still has its base type inferred from the header.
- Operations: the result of an operation of `N` is a value of `N`, checked against each conjunct, and so is a literal operand; an operand of type `raw N` other than a literal makes the operation raw, and nothing in it is checked.
- `raw N` is ghost, since a compiled value of `N` satisfies its invariants, which is what lets the native type be read from them (`NativeTypeAnalysis.ConstraintOf`, from the header's constraint and `N`'s own invariants). `raw N` compiles as `N`, and a compiled witness is `N`'s default value.
- Verification: the derived declaration is translated as a subset type of `raw N`, its well-formedness check covering the header's constraint and the witness, the default included, against every conjunct. Fixes newtypes needed: the where clause of a variable of type `N` assumes the `CanCall`s of the invariant functions it calls, as the `$Is` axioms of a subset type do; the check of an operation's result applies each invariant to a value of `raw N<A>`, which crashed on a generic newtype; and, with the option, the check against the base type of a generic newtype instantiates the base, and a base type that was inferred is normalized so that its constraint is checked (https://github.com/erniecohen/dafny/issues/495 is the same bug without the option).
- The exit test (section 10) gave each newtype of a sample of 97 existing tests `invariant true` and compared the verdicts with the option on. It found the fixes for a generic base and an inferred base, and the rule for a base that may be empty; what remains is an extra related location at the header's conjunct, a different choice among several failing conjuncts, and more procedures verified.
- `ff1f25695`, a fix of `d22e4578c` that the option-off identity sample found: the header's conjunct shares a header variable of inferred type with the newtype, and the pre-type resolver kept that variable's pre-type on a condition that any let resolved a second time also met, such as the one a datatype update becomes. Without the option that changed the numbering of type proxies in an error message of `dafny0/DatatypeUpdate.dfy`. The resolver now keeps the pre-type only of the header variable of the derived declaration it is resolving.
- `87ca94c53`, on its own: the expected verdicts of `trait-gates`, which the gate no longer stops, of `verify`, whose RUN line is now its first line, and of the new `newtypes`, `newtype-generics`, `newtype-witness-checks`, `newtype-compile` and `newtype-trait-cast`, with the reason each moved.
- `124176d7f`: controls for the four sites of `d22e4578c` that the whole-suite mutation run found no test sensitive to, each at the end of an existing test, and each shown to change that test's result under a local mutant of its site. `newtype-witness-checks` gains `EarlierConjuncts` (an invariant's well-formedness assumes the header's constraint) and `BaseInvariants` (a default witness that fails the base type's invariant, and the message naming the witness tried); `emptiness-errors` gains `HeaderWitnesses` and `ProvidedNewtypes` (the header's witness clause gives a provided newtype its characteristics) and `RawNewtypes` (`raw N` is as inhabited as the base type, controlled at resolution, where the verifier does not see it).
- `01babcb9c`, on its own: the verdict of `newtype-witness-checks`, which those controls move, with the reason.
- `dd7eaa6e9`, the plan only: the step-8 rows of the table of sites, with the results of the mutation run against the whole suite (see "Controls").

**The owner's design questions.** `f585bda9b`, the verdict in `4007fda2a`.
- `f585bda9b`, the plan and a test, no source change. Question 15 of section 8, invariants that a proof must invoke explicitly: the design has them already, as an invariant that calls an opaque predicate, whose conjunct is the atom; section 6.1 gets the example, and the new test `opaque` shows formation with and without the reveal, uses, an opaque member, a raw opaque member, `hide`, a newtype's witness and variance. It records a gap under `--default-function-opacity autoRevealDependencies`, to be fixed in step 9. Question 14, invariants that carry a proof (`invariant Q by { ... }`): recommended as a stage of its own, with the rules a probe showed are needed against circularity. Question 8 notes that a witness member covers a refinement's added witness.
- `4007fda2a`, on its own: the verdict of `opaque`, with the reason.

## Option off

Without the option nothing is derived, `raw`, `invariant` and `witness` stay identifiers, the extension check never runs, and what step 8 changed for newtypes applies only with the option (the instantiated base type of a generic newtype) or only to a newtype with invariants (the rest). Until step 8 the gate and the cache's step-aside also returned at once; the fix of the conversion to a trait (`9f6253ad4`) is the one change without the option, and only for programs that failed before. Each step was compared with its base on a sample of the lit tests: `resolve` output, legacy `/noVerify` output, and `/print` Boogie.
- Step 1: three disjoint samples of every twelfth test (193, 194 and 194 files).
- Step 2: a fourth (195 files).
- Step 3: a fifth, plus every fourth test of https://github.com/erniecohen/dafny/issues/143 (207 files). Boogie was printed both under the legacy type system and under `--type-system-refresh --general-traits=datatype --general-newtypes` (719 Boogie files).
- Step 4: a sixth, plus another fourth of the tests of issue 143 (207 files, 401 Boogie files), the same way.
- Step 5: a seventh (262 files, 829 Boogie files), the same way.
- Step 6, against the base after the latest merge: the step-5 sample, the new tests of the base, and 80 tests drawn from those that use general traits or type tests (329 files, 994 Boogie files). The program printed after parsing (`/dprint`) and after resolution (`/rprint`) was compared too, for the parser and printer fixes (620 printed programs).
- Step 6a, against the base after the latest merge: the step-6 sample, the base's new tests, 80 tests drawn from those that declare a variance annotation, and every test that uses `raw` as an identifier (428 files, 1218 Boogie files, 818 printed programs). The only differences are the known ones below: `git-issue-623.dfy`'s crash and `github-issue-27.dfy`'s order.
- Step 7, against the base after the latest merge: the step-6a sample, the base's new test, and 85 tests drawn from those that refine a module, declare an abstract type, export by `provides`, or use `--synonym-refines-abstract-type` or `--legacy-unchecked-import-binding` (513 files, 1588 Boogie files, 983 printed programs). Again only the known differences, and one of the same kind that is new to the sample: `git-issue-611.dfy`, a test of the legacy type system, fails an assertion of the resolver under the refreshed one, on the base too.
- Step 8, against the same base: the step-7 sample and every test that declares a newtype (767 files, 2115 Boogie files, 1477 printed programs). Only the known differences, once `ff1f25695` fixed the one difference the sample found (the numbering of type proxies in `dafny0/DatatypeUpdate.dfy`).

The counts for steps 5, 6 and 6a are corrected here: those samples named 5, 10 and 23 files twice, under two spellings of their paths, and the earlier counts included the repeats.

All identical, apart from crashes that the base has too and whose messages differ only in paths, line numbers or compiler-generated names: https://github.com/erniecohen/dafny/issues/416, https://github.com/erniecohen/dafny/issues/417, and `git-issue-623.dfy` and `git-issue-611.dfy` under the refreshed type system (https://github.com/erniecohen/dafny/issues/492); and `github-issue-27.dfy`, whose Boogie errors (on the base too) come out in a different order. The tests of issue 143 keep their verdicts.

## Tests

`git-issues/git-issue-105-*.dfy`, all listed in `.github/review/regressions`.
- Step 1: `option`, `syntax`, `raw-identifier`, `control`, `refinement`, `opacity`, `reads`, `two-state`, `modifiers`, `codatatypes`, `not-yet`, `verify`, `libraries`.
- Step 2: `raw-types`, `raw-errors`, `raw-exports`, `raw-extends`, `raw-refinement`, `raw-library`, `raw-compile`.
- Step 3:
  - `datatypes`: sections 6.1 and 6.3, and the controls V1, V4 and V8;
  - `vacuity`: V5 and V5b;
  - `extension`: section 6.2 and each clause of the condition;
  - `compile`: witnesses as default values in C# and Python.

  V7 is the declaration rule, tested since step 1.
- Step 4:
  - `generics`: V1 and V4 over a type parameter, conversions between instantiations, witnesses;
  - `variance`: the variance a declaration may give;
  - `emptiness` and `emptiness-errors`: the datatype rows of the vacuous-types table (section 5.3), and `dafny audit` of bodiless and extern functions;
  - `vacuity` adds V5c, `extension` the generic cases of each clause, `compile` generic witnesses, and `raw-compile` is now verified first.

- Step 5:
  - `classes`: sections 6.1 and 6.3 for classes, the controls V2, V6 and `Bad`, and each place a raw reference may and may not go;
  - `class-errors`: section 6.2's class examples, the constructor rule, ghost type tests, and section 3.3's lambda bodies;
  - `class-witness`, `class-compile` (type tests and raw references in C# and Python), `exports` and `class-exports` (export sets for datatypes and classes);
  - `not-yet`, `raw-errors`, `verify`, `opacity` and `refinement` change because classes are no longer gated, and `emptiness` adds a class with a bodiless constructor.
- Step 6:
  - `traits`: reference traits, with V3, the check at `new;` of inherited invariants, a diamond, a trait of another module, raw receivers and joins;
  - `general-traits`: general traits, with the values of `raw G`, generic traits, inherited witnesses, and a class that extends a general trait;
  - `trait-errors`: the resolver's rules, the paradoxes of section 3.3 built through a trait, ghost type tests, and a witness with no invariant;
  - `trait-witness`, `wellformedness` (the order of the assumptions), `trait-compile` (trait values and type tests in C# and Python) and `trait-gates` (what stays behind the gate);
  - `not-yet`, `raw-errors`, `opacity`, `verify`, `raw-extends` and `refinement` change because traits are no longer gated, and `syntax` adds members after bodiless ones.

- Step 6a:
  - `variance`: the polarity rules, one rejected declaration for each, and the declarations accepted only because `D<X>` keeps the declared variance;
  - `generics`: the positive polarity controls (a covariant `L<nat>` used as an `L<int>`, a contravariant `N<int>` as an `N<Empty>`, each verifying only because the conversion is a subtyping), conversions against the variance, and `Components` (membership in the raw type);
  - `statements` (new): statements inside expressions;
  - `classes` adds `RawQToCell`, `TestRawObject` and `TestRawObjectChecked`, and `trait-witness` adds `TwoWitnesses`.
- Step 7:
  - `refinement-verify`: inherited sites, with added invariants checked and inherited ones assumed (a failure of the base is not reported again), constructors with an explicit and an implicit `new;`, an invariant added to a trait, an abstract import bound to a refinement, the witness rules, members that became raw (a body and a by-method), an unsplit check, and a refinement of a library base;
  - `abstract-types`: the characteristics as promises, witnesses in refinements, layered refinements, traits, and the export of an abstract type; `abstract-errors`: the resolver's rules; `abstract-synonym`: `--synonym-refines-abstract-type`;
  - `datatypes` adds `ByMethods`, the controls of the by-method fix; `trait-gates` now holds only newtypes; `not-yet`, `raw-errors`, `raw-exports` and `class-exports` follow the gate and the new message.
- Step 8:
  - `newtypes`: the member form, the old syntax with invariants, members, traits, refinement, export, and real and bitvector bases;
  - `newtype-generics`: generic newtypes, inferred header variables, a base of type `bool`, and a generic newtype based on one with invariants;
  - `newtype-witness-checks`: the header's checks, the default witness, an inferred base and a base that may be empty, and the controls of the header's constraint as an assumption and of a base type's invariants; `newtype-witness-errors` and `newtype-errors`: the resolver's rules;
  - `newtype-native` (native types), `newtype-compile` (compiled code in C# and Python), and `newtype-trait-cast` (the conversion to a trait, without the option);
  - `verify` and `trait-gates` now verify every module, each with negative controls; `raw-errors` and `not-yet` follow the gate's removal;
  - `emptiness-errors` adds the characteristics of `raw N` and of a provided newtype in the old syntax.
- The owner's design questions: `opaque` (new), invariants that call opaque predicates.

`datatypes`, `vacuity`, `compile`, `generics`, `emptiness`, `raw-compile`, `classes`, `class-compile`, `exports`, `traits`, `general-traits`, `wellformedness`, `trait-compile`, `trait-gates`, `statements`, `refinement-verify`, `abstract-types`, `abstract-synonym`, `verify`, `newtypes`, `newtype-generics`, `newtype-witness-checks`, `newtype-compile`, `newtype-trait-cast` and `opaque` verify first, so they have rows in both `expected-verdicts.tsv` files. Unit tests: `DooFileTest.MemberInvariantsRecordedOnlyWhenOn`, `ModuleTranslationCacheTests.AMemberInvariantInAnUnrelatedFileKeepsReuse` (an invariant of any kind of type in an unrelated file, newtypes in both syntaxes included, leaves a module's entry reused; until step 8 a newtype stopped reuse), `AModuleWithASupportedMemberInvariantIsReused`, and, since step 6a, `ResolutionPipelineTests.MemberInvariantsRequireTypeSystemRefresh` in `DafnyCore.Test` and `MemberInvariantsTest` in `DafnyLanguageServer.Test` (`DiagnosticsStatusesHoversAndSymbols`, `VerificationOnSave`, `OptionsFromTheProjectFile`).

## Controls

The owner's rule for every feature: every addition (an axiom or assumption) has a positive control, a test that verifies only because the addition is there, and every check a negative control, a test rejected exactly at that check. A local mutation run disabled each site in turn, rebuilt, and ran all the `git-issue-105` tests: "Flips" lists every test whose result changed. Until step 6a the run covered only the tests predicted for each site, so the lists given then were incomplete, and the claim that no other test changed had not been checked; since step 7 each site is run against the whole suite, and step 8 ran every site again, those of the earlier steps included, on the tree with newtypes. The tables are under *Controls* in section 10 of the plan.
