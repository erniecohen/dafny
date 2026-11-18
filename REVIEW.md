# review/4.11.0: Dafny 4.11.0 with soundness fixes

This branch is Dafny 4.11.0 (tag `v4.11.0`, commit `fcb2042`) with fixes for
eight soundness issues filed against dafny-lang/dafny, one commit per fix, and
it builds against Boogie 3.5.5 with a fix for a ninth, filed against
boogie-org/boogie.  Since `4.11.0+fcb2042d.review.256a1ab3` it also has fixes
for twelve issues filed here: one more soundness fix, crash and non-termination
fixes, a diagnostic fix and an auditor fix, one pull request each (below).
A build of it reports the version `4.11.0+fcb2042d.review.d2070b81`, and the tag
of the same name with a `v` marks it: `v4.11.0+fcb2042d.review.d2070b81`.

It is the shipped line.  Improvements go to the branch `dev`, which follows
upstream's `master`, and come back here only after they pass the tests.

Where the fork is going is in [`ROADMAP.md`](ROADMAP.md); the work itself is in
[the issues](https://github.com/erniecohen/dafny/issues).  How to work here, for
anyone, human or AI, is in [`AGENTS.md`](AGENTS.md).

## The fixes

| fix | test and release note | issue | what it changes | kind |
|---|---|---|---|---|
| `b6116c9` | (upstream's, in the fix) | [#6366](https://github.com/dafny-lang/dafny/issues/6366), fixed upstream by [#6367](https://github.com/dafny-lang/dafny/pull/6367) after 4.11.0 | An automatic induction hypothesis, and the range of a forall call statement, assume their call permissions only under the bound variables' type antecedent.  Upstream's commit, cherry-picked with its author. | weakens only |
| `cf5fdba`, refined by `5fc27c0` | `b7231f8`, `f102115` | [#6531](https://github.com/dafny-lang/dafny/issues/6531) | The equality axiom of a datatype with a single constructor gets the antecedent `Ctor?(a) && Ctor?(b)` in its direction "equal fields imply `Dt#Equal`", which ranged over every datatype value.  The other direction, "`Dt#Equal` implies equal fields", holds of every value, since `Dt#Equal` is equality, and stays unguarded.  `cf5fdba` guarded both directions; `5fc27c0` guards only the false one, as the pull request [dafny-lang/dafny#6540](https://github.com/dafny-lang/dafny/pull/6540) does, because guarding both made some proofs that compare tuples hundreds of times dearer. | weakens only: it still only adds a guard, now to one direction of the axiom |
| `f621876` | `251fbd1` | [#6537](https://github.com/dafny-lang/dafny/issues/6537) | Membership in `Map#Items` and `IMap#Items` gets a first conjunct: the item is a pair. | **asserts** that every item is a pair |
| `007b409` | `9e7df43` | [#6533](https://github.com/dafny-lang/dafny/issues/6533) | The assumption after a forall statement that assigns to the heap puts the bound variables' type antecedent outermost, around the call facts of its range. | weakens only |
| `5ecb3b4` | `c0713af` | [#6532](https://github.com/dafny-lang/dafny/issues/6532) | `BplForallTrim` keeps a bound variable whose type may be empty, under an `exists` guard, instead of dropping it.  Also substitutes the type arguments in the call facts of a constant field's right-hand side, which the first change exposed. | weakens only |
| `2ce2b7a` | `ca79a16` | [#6534](https://github.com/dafny-lang/dafny/issues/6534) | Deletes the unguarded `$Box($Unbox(x): T) == x`. | **asserts** `$IsBox`-guarded inverses for `ORDINAL` and `Field`, and `$IsBox` of the value a field holds in the heap |
| `b0f99c7` | `d67ac3e` | [#6535](https://github.com/dafny-lang/dafny/issues/6535) | `Map#Elements(Map#Glue(a, b, t)) == b` becomes pointwise inside the domain `a`; the same for `IMap#Glue`. | weakens only |
| `7512bd5` | `0861414` | [#6536](https://github.com/dafny-lang/dafny/issues/6536) | The guard of the ORDINAL `(o - m) + n` axiom, `n <= ORD#Offset(o) + m`, becomes `m <= ORD#Offset(o)`. | **asserts**: the axiom now also applies where it is true and did not before (`m <= Offset(o) < n - m`) |
| `a48933e` | `3ff7e36` | [boogie-org/boogie#1168](https://github.com/boogie-org/boogie/issues/1168) | Dafny builds against Boogie `3.5.5-review.37e4435d`, Boogie 3.5.5 with the fix (below).  Under the arguments type encoding, which Dafny uses, Boogie no longer emits the reverse casts `B_2_U(U_2_B(x)) == x`, no longer retypes a quantifier's `int` or `bool` variables to `U` for its triggers, and passes a value of a built-in type to a `U` position as a cast. | weakens only: it removes axioms, and the cast rewrites a term to one of the same value |

Issue numbers are dafny-lang/dafny's, except boogie-org/boogie#1168.  "Weakens
only" means that the fix removes an axiom or an assumption, or puts a guard on
one.  "Asserts" means that it also states a fact that was not stated before;
those parts are the ones a reviewer should check.  Each commit message says
what its fix changes and why.

## The fixes since 256a1ab3

Each is one squash-merged pull request here, with its regression test
`git-issues/github-issue-<issue>.dfy` and its release note
`docs/dev/news/<issue>.fix` in the same commit.  Issue numbers are this
repository's.  A crash, non-termination or diagnostic fix changes only programs
that crashed, hung or were diagnosed; it changes no verdict of a program that
already worked, as `AGENTS.md` requires.

| fix | issue | what it changes | kind |
|---|---|---|---|
| `36811f3` (#52) | [#40](https://github.com/erniecohen/dafny/issues/40) | Cloning a such-that assignment with missing bounds inside a `match` case no longer crashes the resolver. | crash |
| `8ef1431` (#53) | [#41](https://github.com/erniecohen/dafny/issues/41) | Allocating an object with a named constructor inside a `match` case no longer crashes the resolver. | crash |
| `d7b2b63` (#54) | [#42](https://github.com/erniecohen/dafny/issues/42) | Ghost inference covers locals initialized by `decreases to` and `nonincreases to` expressions, which crashed the resolver. | crash |
| `da1d421` (#55) | [#35](https://github.com/erniecohen/dafny/issues/35) | The auditor no longer reports bodyless instance members of traits as missing-body assumptions; static members and explicit assumptions are still audited. | auditor output |
| `64e5544` (#56) | [#46](https://github.com/erniecohen/dafny/issues/46) | Selecting a static member of a bitvector-based newtype under `--type-system-refresh` and `--general-newtypes` no longer crashes the resolver. | crash |
| `3ab6f38` (#57) | [#37](https://github.com/erniecohen/dafny/issues/37) | Type errors involving ambiguous arrow types show arrow syntax instead of internal type names. | diagnostic text |
| `cc10f9a` (#58) | [#43](https://github.com/erniecohen/dafny/issues/43) | An undeclared destination of a `:-` statement is reported instead of crashing, in both resolvers. | crash |
| `d24ac25` (#59) | [#45](https://github.com/erniecohen/dafny/issues/45) | The refreshed resolver resolves a datatype's signature before matching a qualified constructor's arguments, which crashed. | crash |
| `b796eff` (#60) | [#25](https://github.com/erniecohen/dafny/issues/25) | Refining a module with a `:|` whose constraint calls a function no longer crashes: the substituted trigger keeps its parsed form and heap label. | crash |
| `7959732` (#61) | [#39](https://github.com/erniecohen/dafny/issues/39) | The receiver of a static call written through an object, as in `(assert false; c).f()`, is checked inside a `match`, a quantifier, a set or map comprehension and a lambda, as it is elsewhere; cloning and substitution dropped it. | **soundness**, weakens only: it restores well-formedness checks and asserts nothing |
| `9af9f84` (#62) | [#44](https://github.com/erniecohen/dafny/issues/44), program (a) | A cyclic type synonym used by a later datatype or codatatype is reported instead of making the resolver run forever. | non-termination |
| `b97a6fe` (#64) | [#26](https://github.com/erniecohen/dafny/issues/26) | The existence check of a `:|` that binds many variables no longer overflows the stack: a long disjunction is balanced, and at most 20,000 partial guesses are kept. | crash; weakens only, since a dropped disjunct only strengthens the check |

The fix for #39 moves one verdict of the suite, `dafny2/SnapshotableTrees.dfy`,
from 102 to 103 verified with the same errors: a `reveal` inside a `match` case
now has its receiver checked, which adds one proved assertion batch.  No other
fix moves a verdict of the suite or of the standard library.  #63 moved a test
input under `Inputs/` and changed no product file.

## Self calls in a function's own postcondition ([#166](https://github.com/erniecohen/dafny/issues/166))

A function's own postcondition is checked under a call-permission premise that keeps the
self-call allowance: a self call `F(i)` contributes `i == i || F#canCall(i)`, so the check
cannot assume F's own, unproved, contract.  Two call sites dropped the caller's options under
default options and assumed an unconditional `F#canCall(i)` instead: the body of an exact let,
and the application of a `seq(n, init)` initializer.  So
`ghost function F(i: int): int ensures var unused := 1; F(i) == 0 { 1 }` verified, as did
`ensures seq(1, j => F(i))[0] == 0`, and a lemma then proved `false`.  The fixes, in
`Source/DafnyCore/Verifier/BoogieGenerator.LetExpr.cs` (`LetCanCallAssumption`) and
`BoogieGenerator.ExpressionTranslator.cs` (`CanCallAssumption`, the `SeqConstructionExpr`
case), pass the caller's options at both sites, without the arrow allocation facts that those
sites omitted before.  Their kind is **weakens only**: the assumed premise becomes the existing
allowance, and no fact or axiom is added; `--consistent-obligation-checks` is unchanged.  The
third site of the same shape, a constant field's right-hand side, cannot carry such a self
call: the resolver rejects the cycle it needs.  The regression is
`git-issues/github-issue-166.dfy`, which runs the issue's program, nested, used and pattern
exact lets, the `seq` initializer, valid contracts and a false control under all four
combinations of `--type-system-refresh` and `--additional-axioms`; the release note is
`docs/dev/news/166.fix`.  No verdict of the suite or of the standard library moves.

## Bitvector rotations ([#147](https://github.com/erniecohen/dafny/issues/147))

Z3 5.1.0 composes the counts of nested `ext_rotate_left`/`ext_rotate_right` modulo `2^W`
instead of modulo `W`, which is unsound when the width `W` is not a power of two: Dafny
4.11.0 with Z3 5.1.0 proves `x.RotateRight(a).RotateLeft(b) == 4` for `x == 1 as bv3`,
`a == 3`, `b == 0`, where the value is 1.  The fix, in
`Source/DafnyCore/Verifier/BoogieGenerator.cs` (`AddBitvectorRotateFunction`), defines
`LeftRotate_bvW` and `RightRotate_bvW` by shifts at positive widths that are not powers of
two, and keeps the native operators at width 0 and at powers of two.  Its kind is **asserts**: at
those widths it drops `ext_rotate_*` and asserts the definition by shifts as the meaning of the two
function symbols, which a reviewer must check.  The argument,
the regression `git-issues/github-issue-147.dfy` (with `github-issue-147-emission.dfy`), the
validation and the cost are in
[`docs/dev/bitvector-rotation-soundness.md`](docs/dev/bitvector-rotation-soundness.md); the
release note is `docs/dev/news/147.fix`.

## Opt-in statement-expression conclusions ([#96](https://github.com/erniecohen/dafny/issues/96))

`--statement-expression-conclusions` (default false; also `statement-expression-conclusions =
true` in `dfyconfig.toml`) is a completeness option.  In the condition of an `assert` statement,
it translates a Boolean statement expression `S; E` under quantifiers and connectives as
`C ==> E` (`C && E` at negative polarity), `C` being the statement's conclusion.  Before, only
`S; E` at the top of the condition got `C`, so `assert forall n: nat :: assume false; false`
failed.  Its kind is **neither**: it adds no axiom and no assumption, and replaces the checked
condition by one with the same value wherever the condition's well-formedness check, made just
before in the same state, has established `C`.  It applies only in the current heap: not inside
`old`, `old@L`, `unchanged` or `fresh`, lambdas, set and map comprehensions, let-such-that and
binding-guard if-then-else, nor to an assert statement inside a statement expression.  The
first version of the pull request also rewrote quantifiers under `old` and proved
`ensures false`.  Postconditions, invariants, preconditions and inlined function bodies are not
rewritten.  `docs/dev/statement-expression-conclusions.md` has the argument and the exclusions.
With the option off, the Boogie program is unchanged and no verdict of the suite or of the
standard library moves.  The regression is `git-issues/github-issue-96.dfy`; CI's job
`Statement-expression conclusions (opt-in, #96)` runs its fixtures with the option on and off.
The release note is `docs/dev/news/1371.feat`.

## Opt-in call facts in quantified checks ([issue 196](https://github.com/erniecohen/dafny/issues/196))

`--quantified-check-call-facts` (default false; also `quantified-check-call-facts = true` in
`dfyconfig.toml`) is a completeness option.  A function's definition axiom is guarded by its
`#canCall`, and for a quantified condition the call facts are assumed as a separate quantifier
with the condition's triggers.  When the solver simplifies the checked instance until no trigger
term remains (`abs(l[i] - l[i]) <= d` becomes `abs(0.0) <= d`), that quantifier never fires and
the definition is lost.  With the option, where a method's postcondition is checked at the exit
of its implementation, or the condition of an `assert` statement after its well-formedness
check, a positive universal quantifier `Q = forall b :: T(b) ==> P(b)` is checked as
`forall b :: CC(b) ==> T(b) ==> P(b)`, `CC(b)` being the body of the call-fact quantifier
assumed just before (type antecedents of `b` imply the call facts of the range and the term),
and `Q` itself is assumed afterwards, as before.  Its kind is **neither**: it adds no axiom and
no assumption, since the assumed `forall b :: CC(b)` and the checked quantifier together imply
`Q`.  Loop invariants, function postconditions, preconditions, quantifiers proved by induction,
quantifiers Dafny splits by trigger and conditions rewritten by
`--statement-expression-conclusions` are not affected, nor is anything under
`--consistent-obligation-checks`.  With the option off, the Boogie program
is unchanged and no verdict of the suite moves.  With it on, no verdict of the suite moves, and
2.7% of its declarations change resource count (most within 5%, a few by up to about 3x).
The regression is `git-issues/github-issue-196.dfy`: the issue's program and its variants, with
and without the option, under both resolvers, and negative and `assert false` fixtures.  The
release note is `docs/dev/news/196.feat`.

## Declared lambda preconditions under `--additional-axioms` ([#97](https://github.com/erniecohen/dafny/issues/97))

Dafny's `RequiresN` schema for a lambda value gives only the sufficient direction of its declared
precondition, `r[heap, args] ==> RequiresN(..., HandleN(h, r, rd), args)`, so
`const f := (x: int) requires p(x) => x` could not prove `f.requires(x) ==> p(x)`.  With
`--additional-axioms`, `LambdaCanCallAssumption` (`BoogieGenerator.ExpressionTranslator.cs`) adds,
beside a source lambda's existing guarded facts and under the same family, formal type and
allocation, good-heap and formation-heap guards, plus guards on its captured values and heaps,
`RequiresN(...) <==> Range`, triggered only on the requires selector.  It binds the same variables
as the existing quantifier, including the selector type variables that `--heap-free-lambda-facts`
introduces.  Its kind is **asserts**: the necessary direction is a new fact.  It holds in the model
that interprets `RequiresN` of a handle as exactly its stored precondition map, which satisfies
the native schema and its frame and arrow-type schemas, and in which the stored map
`FormalTypes && (CanCall(range) ==> Range)` equals `Range` under the guards
([`docs/dev/additional-axioms.md`](docs/dev/additional-axioms.md#declared-preconditions-of-source-lambda-values-97)).
With the option off the translation is unchanged.  The regression is
`git-issues/github-issue-97.dfy`: the issue's program, positive, negative and `assert false`
fixtures under both resolvers and both settings of the option, and with `--heap-free-lambda-facts`;
the release note is `docs/dev/news/97.fix`.  With the option on, two standard-library rows move under
Z3 5.1.0 (`Sequences.Elements` well-formedness to out of resource, `DecodeValidPartialsFrom2PaddedSeq`
well-formedness to correct); the commit that updates their expected verdicts gives the counts.
With the option off, no verdict of the suite or of the standard library moves.

## Rank of arrow-typed datatype fields under `--additional-axioms` ([issue 326](https://github.com/erniecohen/dafny/issues/326))

The existing field rank axioms put the elements of a `seq`, `set` or `multiset` field and the keys
and values of a `map` field below the constructor value, but nothing related an application of a
function-typed field to it, so `function Count(o: Ord): nat` over
`datatype Ord = Zero | Succ(Ord) | Lim(f: nat -> Ord)` failed its termination check at `Count(f(0))`.
With `--additional-axioms`, `AddArrowFieldRankAxioms` (`BoogieGenerator.DataTypes.cs`) adds, for
a field of type `A -> T`, `RANK(ApplyN(.., h, f, bx..)) < DtRank(#C(..f..))` under
`$IsGoodHeap(h)`, `$Is(f, TotalFuncN(..))` and the arguments' types, for a datatype,
type-parameter, `seq`, `map` or `imap` result, triggered on the application and the constructor
term; for `A --> T` the antecedent also has `RequiresN`.  `~>` fields and codatatype fields get
nothing.  Its kind is **asserts**.  The model is the well-founded (possibly infinitely branching)
tree model with ordinal ranks that the existing `imap` field axioms already need
([`docs/dev/additional-axioms.md`](docs/dev/additional-axioms.md#rank-of-arrow-typed-datatype-fields-326)),
which also lists each circular-construction route and why it is rejected.  With the option off
the translation is unchanged.  The regression is `git-issues/github-issue-326.dfy`: the issue's
program, positive and `ensures false` fixtures under both resolvers and both settings, and one
fixture per circular route, with a false-derivation attempt, with the option on.  The release
note is `docs/dev/news/326.feat`.

## Opt-in matching-loop detection through existential witnesses ([issue 202](https://github.com/erniecohen/dafny/issues/202))

Matching-loop detection (`TriggerMatch.CouldCauseLoops`) ignores a term of a quantifier's body
that matches a trigger term up to one variable for another, so that `forall x, y {a(x, y)} ::
a(x, y) == a(y, x)` is not a loop.  It treated `p in s` in `forall q | q in s :: exists p :: p in s
&& q < p` the same way, but `p` is skolemized at each instance of the universal, so each instance
yields a fresh `sk(q) in s` that matches the trigger `q in s` again.  With
`--detect-witness-matching-loops` (default false; also `detect-witness-matching-loops = true` in
`dfyconfig.toml`), `WitnessVariables.Of` collects the variables of the quantifiers nested in the
body that are skolemized when the quantifier is instantiated (an existential in positive position,
a universal in negative position, both where the position is mixed), and
`ExpressionEqModuloExpressionsNotInvolvingBoundVariables` no longer counts a match that puts one of
them in the place of a trigger variable as harmless.  Its kind is **neither**: it changes only
which automatically selected triggers are reported or rejected, as the existing detection does, and
adds no axiom.  With the option off, `WitnessVariables.Of` returns null and the comparison is
unchanged.  The regression is `git-issues/github-issue-202.dfy`: the issue's reproducer, the same
loop through a negated universal and through a negated existential, a quantifier where a
non-looping trigger is preferred, and three nested quantifiers that are not loops and get no
warning.  The release note is `docs/dev/news/202.feat`.

## Opt-in inlined-body call permissions ([issue 198](https://github.com/erniecohen/dafny/issues/198))

A checked call `F(args)` of a function with a body is split into one assertion per checked
conjunct of the inlined body, `F#canCall(args) ==> F($LS($LZ), args) || c`, with
`{:subsumption 0}`.  A call in a later conjunct, such as `b.scoped(n)` in
`a.scoped(n) && b.scoped(n)`, unfolds only under its `#canCall`, which the definition axiom
supplies only under the guard `a.scoped(n)` at the caller's layer `$LZ`, where it does not unfold.
So `size(Bin(op,Var(0),Var(1)),[x,y])` with `requires t.scoped(|s|)` was proved only when an
unrelated term unfolded the first conjunct, and which of two equivalent declarations failed moved
with their order.  With `--inlined-body-call-permissions` (default false; also
`inlined-body-call-permissions = true` in `dfyconfig.toml`), `InlinedBodyCanCall` (`BoogieGenerator.SplitExpr.cs`)
puts the body's `CanCallAssumption` at the conjuncts' layer `$LS($LZ)` as each checked conjunct's
antecedent.  Its kind is **neither**: the antecedent is the existing definition axiom of `F` at the
same `args` and `ly := $LS($LZ)`, and each assertion is already guarded by `F#canCall(args)`, so it
assumes nothing new
([`docs/dev/additional-axioms.md`](docs/dev/additional-axioms.md#call-permissions-of-the-checked-conjuncts-of-an-inlined-body-198)).
It has its own option, not `--additional-axioms`: under the shared option it moved one suite
program and three Z3 5.1.0 standard-library rows from verified to out of resource, and raised the
library's resource count by about 3%, so no gate runs with it on.  The option is tested before
anything is built, so with it off the translation is unchanged.  The
regression is `git-issues/github-issue-198.dfy`: the issue's forms and their variants in two
declaration orders under both resolvers, and a precondition and an `assert false` control that must
still fail.  The release note is `docs/dev/news/198.feat`.

## Datatypes and the traits they extend in one decreases order ([issue 317](https://github.com/erniecohen/dafny/issues/317))

A trait that does not extend `object` had no order in `decreases` clauses, so in a datatype that
extends it, `decreases this` could not be compared with `decreases this` of a trait member called
on a trait-typed field, and the termination check was `assert false`.  With the new option
`--trait-rank-decreases` (`BoogieGenerator.Decreases.cs`), the values of such traits join the
structural-rank family of sequences and inductive datatypes through `BoxRank`:
`CompatibleDecreasesTypesForComparison` accepts the pairs, `RankFunction` returns `BoxRank`, and
`ComputeLessEq` compares two trait values by `BoxRank`.  The comparison takes its types from the
callee's decreases expressions as translated, since a trait-typed formal can receive a datatype
argument.  It is a change to the language's termination order, so it has its own option, and it
has no effect with `--legacy-decreases-order`.  With `--additional-axioms`, a trait-typed field of
an inductive datatype constructor gets the type-parameter field axiom
`BoxRank(field) < DtRank(#C(..))` (`BoogieGenerator.DataTypes.cs`); its kind is **asserts**.  The
model interprets `BoxRank` of a boxed datatype value or sequence as its rank and of any other
value as `0`; every rank axiom holds there, and the comparisons are ordinal comparisons of that
rank, a well-founded component pair that the existing tuple argument lifts
([`docs/dev/design/317-datatype-trait-decreases.md`](docs/dev/design/317-datatype-trait-decreases.md),
which also covers cycles through two datatypes and implementations by classes and codatatypes).
Inference is unchanged: a trait member without a `decreases` clause still needs `decreases this`.
With both options off the translation is unchanged.  The regression is
`git-issues/github-issue-317.dfy`: the issue's program, positive, false-control and downcast
fixtures with each option off and on, trait-value pairs with the option on, and the positive file
under `--legacy-decreases-order`.  The release note is `docs/dev/news/317.feat`.

## Review follow-up (https://github.com/erniecohen/dafny/issues/23)

The glued-map elements guard now uses the glued map's domain. The domain axiom
makes this equivalent to the old guard on the input domain; no new fact is
asserted. Both the core and generated preludes use it. The datatype-equality
refactor selects the existing trigger and body in one conditional, preserving
the asymmetric guard. Its equivalence is checked by comparing the generated
Boogie on suite samples with one fixed prelude.

The `github-issue-23-*` regressions cover the reviewer-supplied Box/Unbox
contradiction, ORDINAL subset inverse, undefined ORDINAL subtraction, empty-type
lambda, generic constant initializer and simplified glue example, in both
resolver modes. They also retain the exact `Pos`/`F` lambda cost example.
Unlike the older reviewed implementation, the finite-support repair
(https://github.com/erniecohen/dafny/commit/854f5a65a50658c8c415629d67d0b1bd34860fed)
uses `LambdaCanCallAssumption`, which
exports source lambda facts under the formal type and allocation guards and
matches them at the source family's selectors. At `f(5)` the actual argument
satisfies `Pos`; it need not have a declared witness to instantiate those facts.
This is a difference between encodings, not evidence that a `witness *` type is
always inhabited. The `ViaLambda` negative control keeps the empty-domain case.

`.github/workflows/issue23.yml` records a CI-only A/B probe: one fixed baseline
binary and the same source inputs with only the two prelude guards changed,
Z3 5.1.0, the suite's normal 16M limit and the library's own limits with
`/deterministicLiteralHashes:1 /restartProver`. Full source, per-VC logs and
verdicts are retained. Probe completion alone is not an acceptance gate: its
recorded exits, verdict differences, resource counts and exact Boogie comparison
must be inspected alongside the normal review gates.

## Compatible decreases comparison ([issue 264](https://github.com/erniecohen/dafny/issues/264))

The default termination check now lifts compatible strict/weak component orders
right to left, with TOP padding. A fractional real decrease or an equal
sequence/datatype rank cannot discard an increasing suffix. Numeric lower
bounds are inside the component relations, retaining negative terminal values.
Calls, loops, overrides, termination-ordering expressions, and the experimental
modular global/callback checks share the construction. No new primitive axiom
is added; the loop's assumed weak summary follows from the component laws, and
the changed induction hypotheses follow from the well-founded strict lift.
The [soundness argument](docs/dev/decreases-order.md) covers each component
family and the lift.

This soundness fix ships enabled by explicit owner decision. The hidden
`--legacy-decreases-order` switch restores the former, unsound translation for
comparison and migration. `--modular-termination` remains opt-in. The regression
is `git-issues/git-issue-264.dfy`. Actions records default/legacy verdict and
resource comparisons for the suite and standard library, and byte identity
between the pre-fix product and candidate legacy mode. The [comparison results
and proof annotations](docs/dev/decreases-order-results.md) list every changed
declaration outcome and explain the baseline updates.

## Boogie

Boogie's unguarded reverse casts,
[boogie-org/boogie#1168](https://github.com/boogie-org/boogie/issues/1168), are
fixed in Boogie.  The public fork [erniecohen/boogie](https://github.com/erniecohen/boogie)
carries the fix on its branch `review/3.5.5`, which is v3.5.5 with the fix commit
(its message says what changes and why the result is sound), its tests, and CI
that runs Boogie's own test jobs and packs Boogie's NuGet packages.  The same fix
is offered to Boogie's master as
[boogie-org/boogie#1169](https://github.com/boogie-org/boogie/pull/1169).

This branch builds against that fork's release `v3.5.5+review.dcd726f6`, whose
packages have the version `3.5.5-review.dcd726f6`.  The release carries them with
their sha256 sums, and its `REVIEW.md` says what the fork changes and how its
version is computed.  Besides that fix, it brings:

- **Printer fixes, on by default**, so that a printed program (`--bprint`, which is
  Boogie's `/print`) parses back into the program that was printed.  The printer
  keeps the parentheses that its parser needs
  ([#180](https://github.com/erniecohen/dafny/issues/180)): it printed
  `a && (b && c)` as `a && b && c`, which parses as `(a && b) && c`, and
  `2 * (3 div 2)` as `2 * 3 div 2`, which means `(2 * 3) div 2`.  It writes
  `hideable` before a hideable axiom, which Dafny's function and constant
  definition axioms are
  ([#182](https://github.com/erniecohen/dafny/issues/182)); without it, `hide`
  pruned nothing in a printed program, which could then prove what Dafny does not.
  And it writes the attributes of an if command and of an assignment, such as
  `{:allow_path_isolation}` and `{:id}`
  ([#183](https://github.com/erniecohen/dafny/issues/183)).  Only printed text
  changes: Dafny sends the solver what it sent before.
- **Options that are off by default**, and that Dafny does not set:
  `/deterministicLiteralHashes:1`, `/printDefinitionAxiomsInPlace`, `/verifyAsTasks`
  and `/structuralLiterals`.  The fork's `REVIEW.md` describes them.
- **A fix to a prover's cached background**
  ([#241](https://github.com/erniecohen/dafny/issues/241)): a `Reset` after a
  `FullReset` sent the first context's background instead of the current one's.
  Dafny's task pipeline targets the prover for every split and never reaches that
  path, so its queries do not change.
- **Two more options that are off by default**, `/cacheUsedTypes` and
  `/lockFreeExpansion` ([#241](https://github.com/erniecohen/dafny/issues/241)),
  which take work out of setting up a prover for each split and change no byte
  that the solver gets.  On Std with 4 cores they cut CPU by 13.7 to 16.4% with
  a no-op solver and wall time by 8.6 to 9.4% with Z3 4.12.1, with the same
  verdicts.  Pass them with `--boogie "/cacheUsedTypes /lockFreeExpansion"`; a
  program that loads the standard library cannot yet, because its `.doo` records
  the `--boogie` options it was built with and refuses others.
- **`/cacheBackground` and `/scheduleByImplementation`, off by default**
  ([#241](https://github.com/erniecohen/dafny/issues/241)).  Under
  `/cacheBackground`, a prover whose next split has the same pruned declarations,
  in the same order, seed, type encoding types and SMT options as the context it
  holds restarts that context instead of setting it up again; each solver gets the
  same commands in the same order, so resource counts do not change.
  `/scheduleByImplementation` lets an implementation's splits run one after the
  other on one prover, so that more contexts are restarted; it changes which
  solver process verifies which split, so resource counts can differ.
- **Canonical native-result map payloads**, on by default
  ([#284](https://github.com/erniecohen/dafny/issues/284)): the same-index
  read-over-write axiom requires a native payload's box/unbox round trip to
  preserve the stored value. This weakens the inconsistent row without adding
  an axiom. The hidden `--legacy-noncanonical-map-payloads` switch restores the
  old row for comparison and migration.
- **A pooled-solver idle-handshake repair**, on by default
  ([#287](https://github.com/erniecohen/dafny/issues/287)): a healthy solver no
  longer gets retired merely because its idle handshake exceeds 100 ms. The
  default safeguard is 300 seconds; dead or broken transports still retire
  promptly. No verification condition changes, but retaining physical solvers
  changes their pooled history and can change later batches' resource counts
  and outcomes at a resource limit. No fixed RU decrease is promised.
  Restart-per-batch measurements should be unaffected by that history change;
  native pooled runs can move. Boogie's hidden `/legacySolverIdleTimeout:1`
  restores the old deadline for comparison, passed through Dafny's `--boogie`.

`a48933e` made this branch build against the fork's packages, at
`3.5.5-review.37e4435d`.  It now builds against `3.5.5-review.dcd726f6`:

- `Source/DafnyCore/DafnyCore.csproj` names `Boogie.ExecutionEngine`
  `3.5.5-review.dcd726f6`, and `dotnet-tools.json` names the `boogie` tool at the
  same version;
- `nuget.config` adds a local package source, `Binaries/boogie-packages`, and maps
  every Boogie package to it and to nothing else, so a missing package stops the
  restore instead of falling back to nuget.org's 3.5.5;
- `Scripts/fetch-boogie-packages.sh` downloads the 14 packages from the release
  into that folder and checks each against `Scripts/boogie-packages.sha256`, which
  is the release's `SHA256SUMS`.

The move from `37e4435d` to `a7d160a7` is a product change, and so a new version.
It moves no verdict of the suite or of the standard library.  Its test,
`git-issues/github-issue-182.dfy`, prints a program that uses `hide *` and verifies
the printed program with Boogie, which now reports the error that Dafny reports,
where it used to verify the program.  Its release note is
`docs/dev/news/boogie-printed-programs.fix`.

The move from `a7d160a7` to `c5a9f2ef` is also a product change.  Its test,
`git-issues/github-issue-241-split-setup.dfy`, verifies a program with every
assertion its own split, with and without both new options, and requires the same
report and the same SMT-Lib text sent to the solver.  Its release note is
`docs/dev/news/241-boogie-split-setup.feat`.

The move from `27e27ebc` to `bdb7f929` is a product change, and so a new version.
It brings the repairs for `/typeEncoding:m`
([#281](https://github.com/erniecohen/dafny/issues/281)), from
[erniecohen/boogie#7](https://github.com/erniecohen/boogie/pull/7).  Boogie's
monomorphizer now drops a quantifier that binds nothing, as the other encodings do, and
keeps the `hideable` mark when it splits or instantiates an axiom; its pruning treats
`reveal` of a declared function as revealing that function's instances.  The release also
carries [erniecohen/boogie#10](https://github.com/erniecohen/boogie/pull/10), which
changes only a test of Boogie's.  `/typeEncoding:a`, the default, sends the solver the
same text as before.  Its tests are `git-issues/github-issue-281-empty-quantifier.dfy` and
`git-issues/github-issue-281-hide.dfy`, each run under `:m` and `:a` against one
expected output.  Its release note is `docs/dev/news/281-typeencoding-m.fix`.

The move from `bdb7f929` to `dcd726f6` is a product change, and so a new version.
It brings `/cacheBackground` and `/scheduleByImplementation`, from
[erniecohen/boogie#6](https://github.com/erniecohen/boogie/pull/6); with both off, the
solver gets the same text as before.  Its test,
`git-issues/github-issue-241-cache-background.dfy`, verifies a program with every
assertion its own split on one core, with and without `/cacheBackground`, and requires
the same report and, from the CSV log, the same outcome and resource count for every
check.  Its release note is `docs/dev/news/241-boogie-cache-background.feat`.

**To build this branch, run `sh Scripts/fetch-boogie-packages.sh` once first.**
CI does so in every job that builds.

## Tests, release notes and the manual

Each fix after the first has a follow-up commit, as in upstream's pull
requests:

- a regression test, `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-<issue>.dfy`
  with its `.expect`, named as upstream's own fix for #6366 named its test;
- a release-note fragment, `docs/dev/news/<issue>.fix`, as `docs/dev/README.md`
  describes.

For boogie-org/boogie#1168, `3ff7e36` adds the test
`git-issues/boogie-issue-1168.dfy` and the release note
`docs/dev/news/boogie-1168.fix`, named by description, since the issue number is
Boogie's.

For #6531, #6532, #6533, #6534 and #6535 the regression tests include source
programs that prove `false` under the old encoding and are refused now. In
`github-issue-23-box-bad.dfy`, `Opt<int>` and `Opt<bool>` share the datatype sort;
three distinct integer payloads are unboxed as booleans, and the unguarded
Box/Unbox inverse identifies two of their boxes. The guarded inverse also has
preservation tests for field values and `set<ORDINAL>` subset membership, and a
vacuity control. For #6536, the undefined constant expression `((0 as ORDINAL) - 1) + 1`
reaches the solver: the old axiom proved its postcondition despite its
well-formedness error, whereas the corrected guard rejects both. The existing
defined-arithmetic tests still verify. For #6537 the test checks facts about
items that 4.11.0 did not prove. For boogie-org/boogie#1168 the test checks proofs
about values read from arrays, sequences, maps, fields and sets, which relied on
the reverse casts: they verify under 4.11.0 and with the fix, and fail when the
reverse casts are dropped without the fix's casts.
Boogie's own tests for the fix, on its branch `review/3.5.5`, include the issue's
program, on which Boogie 3.5.5 proves `assert false`.

Three of upstream's tests record the verifier's resource counts in their
expected output (`dafny0/CoinductiveProofs.dfy`, `dafny0/SubsetTypes.dfy`,
`dafny1/SchorrWaite.dfy`).  The fixes for #6534 and #6536 move those counts,
though not any verification result, and `7b6dba7` updates the expected outputs.
The Boogie fix moves those counts again, and `74581b2` updates them the same way.
Otherwise upstream's own harness gives the same outcome with and without the
eight Dafny fixes for every test under `dafny0` to `dafny4`, `git-issues` and
`logger` (run with the C# backend only).

The Boogie fix changes how much work some proofs take.  Upstream's harness was
run for it on the tests named in `.github/review/regressions` only, not on every
test.  There it cost one proof in `dafny4/GHC-MergeSort.dfy`, which was already
dear, and `cf5c56e` repairs it with one assertion, as upstream's own fix for #6366
added hints to that file.  In the suite, at its resource limit, it moves the
verdicts of a few programs, none by a new error: `15d1b11` updates the expected
verdicts, and its message says why each one moved.

The refinement of the fix for #6531, `5fc27c0`, moves no verdict of the suite
and none of the resource counts those three tests record, and `git-issue-6531`
is still refused.

None of the fixes changes the Reference Manual or the command-line help.  They
change the verifier's encoding, not the language or its options, and where the
manual speaks to the point (`m.Items` is a set of pairs; `o - m` is defined only
when the offset of `o` is at least `m`) it already says what the fix makes the
verifier know.  Each follow-up commit gives its fix's reason.

## The version, and how to check it

Since 2026-11-14 the hash covers every product file, those the fork added as well as
those v4.11.0 already has, and leaves out the paths that are not product (tests,
`docs`, `.github`, `friction` and the documents at the top of the tree):

    git diff --no-renames --abbrev=7 v4.11.0 <last product commit> -- . \
      ':(exclude)Source/IntegrationTests' ':(exclude,glob)Source/*.Test/**' \
      ':(exclude,glob)Source/*.Tests/**' ':(exclude)docs' ':(exclude).github' \
      ':(exclude)friction' ':(exclude)REVIEW.md' ':(exclude)ROADMAP.md' \
      ':(exclude)AGENTS.md' ':(exclude)CLAUDE.md' | sha256sum

`.github/review/version.sh` computes it.  The earlier hash, of
`git diff --diff-filter=M`, saw only files that v4.11.0 already has, so a change
confined to a file the fork added kept the version: two different products were
both `4.11.0+fcb2042d.review.e35c4989`.  The versions below were computed the
earlier way.

`d2070b81` is the first 8 hex digits of the sha256 of the change that the
product commits make to files that v4.11.0 already has:

    git diff --diff-filter=M --abbrev=7 v4.11.0 b97a6fe | sha256sum

`b97a6fe`, the fix for #26, is the last product commit.  `.github/review/base`
names `ada0d09`, the same product tree on that pull request's branch before it
was squash-merged; the two give the same hash.  Before the twelve fixes since,
the version was `256a1ab3`, as follows.

`256a1ab3` is the first 8 hex digits of the sha256 of the change that the
product commits make to files that v4.11.0 already has:

    git diff --diff-filter=M --abbrev=7 v4.11.0 5fc27c0 | sha256sum

`5fc27c0` is the last product commit, the last commit that changes a file
the build compiles; here it is the refinement of the fix for #6531, which came
after `a48933e`, the commit that moves Dafny to the fixed Boogie.  Before them,
the line was `4.11.0+fcb2042d.review.c47a78b3`, whose last product commit was
`7512bd5`, the last of the eight fix commits; that build was first named by
applying the eight fixes to a fresh repository of v4.11.0's tree with
`git apply` and hashing `git diff`.
That diff leaves out the files the fixes add, which stay untracked, and
abbreviates object ids to 7 digits; the command above computes the same thing
from a clone of this repository that has the tag `v4.11.0`.  (In a very large
clone git may lengthen an abbreviation that would be ambiguous; a shallow fetch
avoids that.)

**The version names the product, the source that is compiled.**  The commits
after `b97a6fe` add or update tests, release notes, documents and CI.  They
change no product file, so the version stays.  The rule on this branch:

- a commit that changes or adds a product file is a new last product commit,
  and gets a new version and a new tag;
- any other commit keeps them.

The product is everything outside `Source/IntegrationTests/`, the unit-test
projects (`Source/*.Test/` and `Source/*.Tests/`, which no release ships; left out
since 2026-11-16), `docs/`,
`.github/`, `friction/`, this file, and the other documents at the top of the
tree: `ROADMAP.md`, `AGENTS.md` and `CLAUDE.md`.  `.github/review/base` names the base
and the last product commit, and CI's `.github/review/version.sh` computes the
version from them and fails if a later commit changes a product file.  By hand,
this prints nothing:

    git diff --name-only b97a6fe <commit> -- . ':!Source/IntegrationTests' \
      ':(exclude,glob)Source/*.Test/**' ':(exclude,glob)Source/*.Tests/**' ':!docs' ':!.github' \
      ':!friction' ':!REVIEW.md' ':!ROADMAP.md' ':!AGENTS.md' ':!CLAUDE.md'

A release binary built from the tag prints the version with `dafny --version`.
It is built with `-p:SourceRevisionId=fcb2042d.review.d2070b81`.  The version
names the Boogie packages by their version, and `Scripts/boogie-packages.sha256`
names their bytes.

## Friction cases

`friction/` holds small Dafny programs that show where a proof is much harder
than it should be, as candidates for fixes here and as regression checks once
fixed, and tips: workarounds for non-obvious blockers that are accepted as
they are.  Its runner, `friction/run`, checks every case against a given
Dafny binary.  `friction/README.md` describes the cases and how to add one.
The directory belongs to this fork's line only; it is not product, and pull
requests to upstream, which branch from `master`, never carry it.

## CI

`.github/workflows/review.yml` replaces upstream's workflows on this branch and
on `dev`, which exercise every compiler backend.  On this branch it fetches the
Boogie packages, builds Dafny, runs the programs of Dafny's own test suite that
verify (under `LitTest/dafny0` to `dafny4` and `git-issues`) at a resource limit
of 16,000,000 with Z3 5.1.0, taken from Z3's public GitHub release by checksum,
and reports each program's verdict.  It verifies Dafny's standard library,
`Source/DafnyStandardLibraries`, as the library's Makefile's `verify` target does,
once with Z3 5.1.0 and once with the Z3 that the tree's own tests expect (4.12.1 on
this branch), and reports each declaration's verdict.  It also runs the tests named in `.github/review/regressions` in
upstream's own harness.  On a tag it attaches self-contained binaries for `linux-x64` and `osx-arm64`
to a GitHub release, with their sha256 sums.  The `osx-arm64` binary and the release
itself are built on the self-hosted Apple-silicon pool (label `mac-studio`).
The framework-dependent build, artifact reports and portable regression tests
also use this pool, together with JavaScript runtime tests, resolver matrix and unit tests,
finite-support controls, statement-expression controls and verification-batch controls. The test `dafny0/CoinductiveProofs.dfy` declares macOS
unsupported because its expected resource counts differ, so it stays in a
separate hosted Linux job. `.github/review/regression-filter.py` partitions the
standing manifest without dropping or duplicating entries. Every
self-hosted job has an explicit repository/event guard: only pushes, manual
runs, or pull requests whose head repository is `erniecohen/dafny` are admitted.

CI verifies a git tree once.  Both `review.yml` and `obligation-proofs.yml` start with
a job `tree` that takes `git rev-parse HEAD^{tree}` (for a pull request, the tree of the
merge commit that is checked out) and looks for the artifact `verified-<workflow>-<tree>`,
which a run leaves when all its verification jobs succeeded.  It reuses the marker only if
its run, in this repository on a head of this repository, is of the same workflow, has
completed, and succeeded (`.github/review/tree-reuse.py`, tested by `test_tree_reuse.py`).
Then the verifier jobs skip, and the step summary of `tree` links the run it reused.  So the
push of a squash-merge commit whose tree the pull request already verified, and the release tag of
a tree that a push already verified, do not verify again; on a tag, only the build and the
binaries run.  The job `verdict` is the aggregate result: it succeeds when every job
succeeded or was skipped for reuse, and a release requires it.  A pull request reuses only pull
request runs, a manual run on a branch and a pull request from a fork never reuse, and a tree that
differs only in `.github/review/base` is a different tree, because the stamp is the version that
the build checks.  Markers are kept for 90 days; without one everything runs.
Fork pull requests skip these jobs. The obligation-proof matrix, resolution-only
census and pipeline probe have separate hosted fallbacks for other events,
including the obligation matrix's weekly schedule.

The suite verdict gates (all shards, additional axioms off/on) and the Z3
4.12.1 library gates use the guarded Mac pool. Their fixed-source verdicts
matched hosted Linux; the owner approved their measured resource differences
on 2026-11-11. The library job reads the build's harness solver version and
admits only 4.12.1 to that pool; a future harness solver stays hosted until
separately qualified. Z3 5.1.0 library gates remain on Linux because
`Std.Arithmetic.DivMod.LemmaMultiplyDivideLe` changes from `OutOfResource` on
Linux to `Correct` on macOS at the library's existing 1,000,000 RU limit.

Each suite/library job records per-program totals, maximum batch RU,
every batch's resource use and applicable limit, and actual solver binary and
archive hashes in its verdict artifact. Job summaries list every batch using
at least 75% of a finite limit, including overruns. A program's total can span
multiple batches and is never compared with a single batch's limit. Literal
member-level limits (both attribute syntaxes), legacy limits, and resource-limit
multipliers are accounted for; unsupported limit syntax is reported rather
than guessed. These observations do not add resource thresholds to the gates.

A manual `review.yml` run with `platform_parity: true` builds both platforms
at the dispatch's exact commit, runs every suite shard with additional axioms
off/on, and verifies the library under both solvers in both modes. It records
regression and resolver test results too. The `platform-parity-report`
artifact compares verdict columns, resource counts, proof batches, coverage
and job wall times; missing observations are incomplete evidence. Further
platform migrations with resource-only differences require an owner decision;
verdict differences retain the corresponding Linux gate. The frozen
repeated-default-comparison experiment stays on Linux because it pins a Linux
runtime and measures that environment. Self-contained `linux-x64` binaries
also stay on Linux for their execution smoke test.

Jobs in the shared pool set `DOTNET_PROCESSOR_COUNT=4`; suite shards use four
single-core verifier processes and library jobs use `--cores 4`. This bounds
ordinary .NET scheduling to four processors per runner. Every job that can
use the Mac pool first sets its own ephemeral `Runner.Worker` and initial shell
to nice 10, before checkout; subsequent actions and verifier processes inherit
that niceness. The step verifies both values and never changes the listener,
runner installation or service configuration. The test runsettings
limit xUnit to two concurrent tests, since each lit test normally uses two
verifier cores. Solver
archives for both platforms and harness versions 4.12.1 and 4.16.0 are pinned
in `.github/review/setup-z3.sh`. CI installs Java explicitly, and the shared
framework-dependent artifact launches its managed assembly through `dotnet`
on either operating system. No product source or verdict baseline changes.
Published builds normalize source paths, and archives omit Apple metadata and
extended ZIP attributes.
See https://github.com/erniecohen/dafny/issues/256.
`.github/review/release-macos.sh` remains as a manual fallback.

### Expected verdicts

`.github/review/expected-verdicts.tsv` holds the verdict the suite expects for
each program: its exit code, the verifier's summary line (proofs verified,
errors, proofs out of resource) and the lines with errors.  The job `Verdicts`
fails when any verdict changes, or a program is added or removed.  A program's
time and the resources its finished proofs use are not part of its verdict, so
a change in them alone does not fail it; a proof that stops finishing within
the resource limit does.

To change the file on purpose:

1. Push the change.  The job `Verdicts` fails and lists each program whose
   verdict moved, with the expected and the new verdict.
2. Take `expected-verdicts.tsv` from that run's artifact `verdicts-off` (or write it
   with `python3 .github/review/lit-verdicts.py expected verdicts.tsv`), and
   replace `.github/review/expected-verdicts.tsv` with it.
3. Commit it on its own.  The commit message must say, for each program whose
   verdict changed, why: which change made a proof finish or stop finishing,
   or that the program itself changed.  A verdict is never updated without a
   reason on record.

### Expected standard-library verdicts

`.github/review/expected-std-verdicts-z3-5.1.0.tsv` and
`expected-std-verdicts-z3-4.12.1.tsv` hold the verdict the job `Standard library`
expects for each of the library's declarations under that Z3: its outcome
(`Correct`, `Errors`, `OutOfResource`, ...), and for each part of the run (the
library, and its target-specific files for each target language) the exit code,
the summary line and the lines with errors.  They are updated as the suite's
expected verdicts are, from the run's artifact `std-verdicts-off-z3-<version>`, with a
reason for every row that changes.

The library runs with its own resource limits and without a time limit, in
Boogie's default declaration order, as upstream verifies it.  Dafny does not
verify the library again when a program uses it (`--standard-libraries` loads its
prebuilt binary), so these verdicts say whether the library's proofs still go
through on this line, not whether a program that uses the library verifies.  Not
all of them do, and most of those that do not are Z3 5.1.0's: unmodified 4.11.0
verifies every declaration with Z3 4.12.1, and leaves 29 unverified with Z3 5.1.0,
27 of them because a proof runs out of its resource limit.

Boogie's default order sorts declarations by a content hash that, for numeric
literals, came from .NET's process-randomized hashing, so a proof close to its limit
could change its verdict between runs of the same tree
([#186](https://github.com/erniecohen/dafny/issues/186)).  The job therefore
verifies the library part with `--boogie /deterministicLiteralHashes:1`, the same
normalization with deterministic literal hashes, and the expected files were
recorded under it.  It also passes `/restartProver`, which gives each proof batch a
fresh solver process: under Z3 5.1.0 (with `--cores 4`) a batch's resource count depended on which
batches the reused process had checked before it (one batch,
`MulInternalsNonlinear.LemmaMulIsAssociative`, moved by up to 0.17%), so the
resource fingerprint took 7 distinct values in 12 runs
([#30](https://github.com/erniecohen/dafny/issues/30)).  In 6 runs with it and 12 without (Z3 5.1.0,
axioms off), that count was the only difference, and no verdict differed.  The
gate's resource counts are therefore measured under `/restartProver` and are not
those of a user's run, which reuses processes; the `process-history` workflow
(weekly, and on demand) verifies the library three times without it and reports the
distinct count vectors and each differing declaration's spread and distance to the
limit.  The target-specific parts cannot take Boogie options, which
must equal those the library's binary was built with; all 82 of their declarations
verify (`Correct`) in every expected file.  Their counts are in the resource
fingerprint, and so still reuse processes, but none of them differed in the 12 runs
without `/restartProver`.  The suite already runs with
`/normalizeDeclarationOrder:0`, on which `/deterministicLiteralHashes:1` has no
effect; it also reuses a process within each program (`--cores 1`), but its gate
compares verdicts only, and its counts were not measured for this.


### Additional axioms enabled

The suite and source-library gates also run with `--additional-axioms`, using
separate expected verdicts and recording paired OFF/ON resource counts. See
[the standing axiom CI documentation](docs/dev/additional-axioms.md#standing-ci-for-the-shared-option-50)
for scope, retained negative/vacuity controls, resource-order caveats and the
scratch-only baseline-capture procedure. The OFF verdict files remain unchanged.

## Guarded character representations

The character reverse round trip is restricted to `$Is(ch,TChar)`, and character
membership is defined by a valid integer value and a successful reverse round
trip. The valid-integer forward round trip is unchanged. Redirecting character
types retain their parent membership requirement, including in visible-type
obligation checks. The hidden `--legacy-unguarded-char` option restores the old
translation for comparison and migration.

This only weakens the old conjunction of character axioms: the old unconditional
reverse round trip and universal membership imply all three repaired clauses.
For a concrete model of the repaired character fragment, take the erased carrier
to be the integers, both character conversions to be identity, and membership
to be the existing valid-range predicate. The range is the BMP in the legacy
character mode, and Unicode scalar values in Unicode mode. The carrier remains
infinite; invalid raw values need not be characters. The executable model in
`git-issues/Inputs/issue-284-char-model.smt2` checks this interpretation and typed
negative contexts in both modes. It models this fragment, not the entire Dafny
prelude or Boogie encoding.

The four character-range lemmas, checked subset introduction, a failing false
assertion, and Unicode surrogate exclusion are regression controls. See
https://github.com/erniecohen/dafny/issues/284 for the scope and the original
inconsistency argument.

The accompanying library proof changes establish padding-prefix and sequence
decomposition facts explicitly, use existing encoding identities while keeping
bitvector definitions opaque, and separate parser and encoding proof steps.
They preserve the specifications and existing resource limits. Every additional
assertion and lemma call is verified; they introduce no axiom or assumption.

## User-written `{:auto_generated}` and `{:opaque_reveal}` ([issue 325](https://github.com/erniecohen/dafny/issues/325))

Dafny marks the declarations it generates, such as the reveal lemmas of opaque members, with
`{:auto_generated}` and `{:opaque_reveal}`, but a user can write those attributes too. Three
places read the attributes as proof that Dafny generated a declaration. The library printer left
every `{:auto_generated}` member out of a `.doo`, so a user-written member that shadowed an
imported name disappeared and a client proved `false` from the stored contracts. The trait check
let a class leave a `{:opaque_reveal}` trait lemma unimplemented, so a call to it assumed its
postcondition. The auditor did not report `{:axiom}` or `{:verify false}` beside
`{:auto_generated}`. All three now ask `Declaration.IsGeneratedByDafny`: the attribute and an
origin that only Dafny makes. Its kind is **weakens only**; no fact or axiom is added, and a
program that does not write these attributes is unchanged. The hidden
`--legacy-auto-generated-attributes` option restores the old behaviour of all three for comparison
and migration. The regression is `git-issues/github-issue-325.dfy`; the release note is
`docs/dev/news/325.fix`.

## Constants read before `new;` ([issue 339](https://github.com/erniecohen/dafny/issues/339))

The resolver let the first division of a constructor body read any `const` field with an
initializer, on the premise that such a constant does not depend on the object.  It can:
`const s: D := t` reads the field `t`.  The verifier translates the read as the constant's
function of `this`, whose definition axiom equates it with the initializer on the fields' final
values, while before `new;` a field still holds a value that can change, so `t := One(s)` made
`s == t` and `t == One(s)` both hold and proved `false`.  `CheckDividedConstructorInitVisitor` now
reports a read of `this.c` before `new;`, in any context, when `c`'s initializer mentions `this`,
explicitly or implicitly (another field or constant of `this`, an instance function, `this`
itself).  An initializer that reads another constant mentions `this` itself, so no closure is
needed.  Its kind is **weakens only**: it rejects programs and adds no fact or axiom; a program
that does not make such a read is unchanged.  The hidden `--legacy-first-phase-const-reads`
option restores the old rule for comparison and migration.  The regression is
`git-issues/github-issue-339.dfy`; the release note is `docs/dev/news/339.fix`.

## Functions that read the heap, stored inside datatypes ([issue 479](https://github.com/erniecohen/dafny/issues/479))

`HigherOrderHeapAllocationChecker` rejects storing, in a field or an array element, a value whose
type contains a function that reads the heap (`~>`).  Otherwise the function could read itself
through the field and apply itself, and applying a function value gets no termination check.
`ContainsRead` looked only at the type and its type arguments, so `datatype W = W(f: () ~> bool)`
passed as reading-free, and storing `W(knot)`, where `knot` reads the field, proved `false` under
both resolvers.  `ContainsRead` now also searches the constructor arguments of inductive and
coinductive datatypes (tuples among them), and the base types of newtypes and subset types, with
the type arguments substituted.  It stops at arrow types, whose base is the reading arrow, and at
the types of classes, traits and type parameters.  It searches each declaration once.  Since the
type arguments of every occurrence are searched where they occur, a recursive or non-regular
datatype ends the search.  Its kind is **weakens only**: it rejects programs and adds no fact or
axiom.  Among the test suite's files that mention `~>`, it changes resolution only in its regression
and in two inputs of `git-issues/github-issue-326.dfy`, `route-subset` and `route-tilde-field`.
Those inputs store such a function in a datatype to test the rank axioms of arrow-typed fields, and
they now run under the legacy option.  It changes no resolution in the standard library (its
sources, `modular-termination`, `orders` and its examples).  The hidden `--legacy-reading-functions-in-datatypes` option restores the old check for comparison and
migration.  The regression is `git-issues/github-issue-479.dfy`; the release note is
`docs/dev/news/479.fix`.

## Conversions to a newtype with an inferred base ([issue 495](https://github.com/erniecohen/dafny/issues/495))

A newtype declared without a base type, `newtype Y0 = six | Yes(six)`, gets its base by inference;
when `Yes` takes a `NewSix`, the base of `Y0` is the newtype `NewSix`.  A conversion to `Y0` checked
`Y0`'s own constraint but not `NewSix`'s, while every value of `Y0` is assumed to satisfy both: the
newtype keeps the resolver's type proxy as its base, and the walk up the chain of base types in
`CheckResultToBeInType_Aux` did not look through it.  So `3 as Y0` checked `Yes(3)` and never
`6 <= 3`, and a method returning `3 as Y0` with `ensures y as int == 3` proved `false`.  The same
gap covered literals of the newtype and conversions from `int`, `real` and bitvector values.  The
walk now strips the proxy first, so the base's constraints are checked as they are for an explicit
base, under both resolvers.  Its kind is **weakens only**: it adds proof obligations and no fact or
axiom; a program in which no base type is inferred translates exactly as before.  The hidden
`--legacy-inferred-newtype-base-conversions` option restores the old check for comparison and
migration.  The regression is `git-issues/github-issue-495.dfy`; the release note is
`docs/dev/news/495.fix`.

## `allocated` in lambda expressions ([issue 457](https://github.com/erniecohen/dafny/issues/457))

A function, a constant or a redirecting type could not depend on the set of allocated references,
but a lambda expression in a method could mention `allocated(c)`.  The verifier translates a
lambda's body, precondition and reads clause in the heap the lambda is applied in, so its value
depended on that heap's allocation state, while the frame axiom of its arrow equates its values in
any two heaps that agree on its reads frame.  With an empty reads frame, `(x: int) =>
allocated(c)` applied after `c` was allocated proved `false`.  `BoundsDiscoveryVisitor`, which
already rejected quantifiers that depend on the allocation state inside a lambda, now also reports
`allocated(...)` in a lambda's body, precondition or reads clause, under both resolvers, unless it is
inside an `old` (or `old@L`) expression within that lambda; an `old` around the lambda does not
count, since the lambda is still evaluated in the heap it is applied in.  Its kind is **weakens
only**: it rejects programs and adds no fact or axiom.  The hidden `--legacy-allocated-in-lambdas`
option restores the old rule for comparison and migration.  The regression is
`git-issues/github-issue-457.dfy`; the release note is `docs/dev/news/457.fix`.

## Override checks in refining modules ([issue 404](https://github.com/erniecohen/dafny/issues/404))

A refining module could strengthen the contract of a trait member, or refine an abstract type into
a class that extends a trait, while it inherited the overriding class member unchanged.  The
override check asserts at the overriding member's origin, which the refining module inherits, and
asserts at inherited origins become assumes, because the base module made them.  So the check
against the new contract was never made, and a call through the trait proved `false`.  Even had it
been made, the pipeline never scheduled a copied member's tasks.  `BoogieGenerator.MustReverifyInheritedOverrideCheck`
now decides when a refining module makes the override check of an inherited member itself: unless
the base counterpart of the member overrode the same member, unchanged (the base counterpart of the
overridden member, or the overridden member itself when it lies outside the refining module).  So a
changed overridden member, a new extension, and a replaced abstract import are checked, and a pair
the module inherits unchanged is not checked again.  Such a check asserts at the inherited origin,
reports the refining module as a related location, and is verified with the refining module even
when the base comes from a library; the pipeline schedules that task, and no other, of the copy.
In the IDE the copy shares the navigation range of the base member, so `IdeState` keeps the tasks of
each `ICanVerify` at a range apart, and the base member's own errors stay shown beside the check.
Its kind is **asserts more**: it rejects programs, by checking an obligation the old rule skipped,
and adds no fact or axiom; a program whose refining modules change no override pair is unchanged.
The hidden `--legacy-elided-override-checks` option restores the old rule for comparison and
migration.  The regression is `git-issues/github-issue-404.dfy`; the release note is
`docs/dev/news/404.fix`.

## Calls added by a statement skeleton ([issue 412](https://github.com/erniecohen/dafny/issues/412))

A refining method that uses a statement skeleton (`...`) inherits the parent's body, and the verifier
assumes, rather than proves again, what that body established, including its postconditions.  That is
sound only if the added statements cannot change state the parent module knows about.
`RefinementTransformer.CheckIsOkayUpdateStmt` enforced this for the left-hand sides of assignments and
for `new` with an initializer that modifies more than `this`, but not for calls: a call parses as an
update statement and becomes a `CallStmt` only during resolution, so the `CallStmt` case of
`CheckIsOkayNewStatement` never sees one.  `...; Set2(c);` after a body that ensures `c.f == 1` let the
refined method ensure `c.f == 2` as well, and a caller proved `false`.  The post-resolution check now
walks the resolved statements and rejects any call (other than the initializing call of a `new`) to a
method or ghost method with a nonempty `modifies` clause, with the error `ref_invalid_call_modifies`.
Its kind is **weakens only**: it rejects programs and adds no fact or axiom.  No program in the suite or
the standard library makes such a call (every refining program in both was resolved with the check), so
the fix costs no proofs.  Still, as for every soundness fix, the hidden
`--legacy-skeleton-modifying-calls` option restores the old rule for comparison and migration.  The regression is
`git-issues/github-issue-412.dfy`; the release note is `docs/dev/news/412.fix`.

## Inherited constructors of classes with new fields ([issue 411](https://github.com/erniecohen/dafny/issues/411))

A refining module may add a field to a class whose constructor it inherits unchanged.  The copied
constructor cannot assign the new field, and its body's definite-assignment check for the field,
at `new;` or at the end of the body, is not at an inherited origin, so it survives as an assert.
But the pipeline never scheduled a copied member's tasks, so `dafny verify` never checked it, and
a read of the uninitialized field proved `false` (the compile commands, which verify the whole
Boogie program, did report it).  `BoogieGenerator.MustCheckInheritedConstructorFields` now decides
when a refining module verifies an inherited constructor: when the constructor has a body and the
module adds an instance field that is subject to definite-assignment rules (one that the base
class's constructed object lacks).  `MustVerifyCopy` joins it with issue 404's
`MustReverifyInheritedOverrideCheck`, and the pipeline schedules the body task of such a
constructor the way it schedules a reverified override check (`IsReverifiedInheritedCheck`).
Every other assert of the copied body stays where refinement put it: asserts at inherited origins
are assumed.  The field checks assert at the base's origin, since a copied `new;` is inherited,
with the refining module as a related location.  A module that adds no such field, or that
declares the constructor itself, gets the same Boogie program as before.
Its kind is **asserts more**: it rejects programs, by verifying an existing assert the old
pipeline skipped, and adds no fact or axiom.  It costs no proof: the inherited body cannot assign
the new field, so a check it adds fails unless the body cannot reach `new;` or its end, and in the
test suite it adds a check only to its regression.  Still, as for every soundness fix, the hidden
`--legacy-inherited-constructor-fields` option restores the old rule for comparison and migration.
The regression is `git-issues/github-issue-411.dfy`; the release note is
`docs/dev/news/411.fix`.

## Replacements that depend on a client ([issue 405](https://github.com/erniecohen/dafny/issues/405))

A module `R` that replaces a replaceable module `F` could import a client `C` of `F`.  The module
graph the resolver checks for cycles has the edges `C -> F`, `R -> F` and `R -> C`, and is acyclic.
But in the compiled program every use of `F` is a use of `R`, so `C` and `R` depended on each other.
A call from `R` into `C` and a call from `C` into `F` each go to an imported module, which the
termination checks take to be lower in the module order, so neither was checked: a lemma cycle
through the two modules proved `false` with no assumption in the program, a function cycle
overflowed the stack, and two datatypes could each need a value of the other.
`ReplacementDependencyChecker`, run by `ProgramResolver` once every module has resolved, identifies
each replaceable module with the modules that replace it and requires the dependencies to stay
acyclic.  A module depends on the modules it imports, on those nested in it and on the one it
refines; the dependency of `R` on `F` by `replaces` is the identification itself and is not counted.
It reads the resolved program, where an import names the module at the end of its path, because the
resolver's own graph names the first component, in which an import of `A.B` depends on all of `A`.
The error is reported at the `replaces` clause and shows a shortest path, such as
`FooImpl -> Bar -> Foo`.  Its kind is **rejects more**: it rejects programs and adds no fact or
axiom; a program without such a replacement, and every program without a replacement at all, is
unchanged.  The hidden `--legacy-replacement-cycles` option restores the old rule for comparison
and migration.  The regression is `git-issues/github-issue-405.dfy`; the release note is
`docs/dev/news/405.fix`.

## Type parameters stored in memory ([issue 481](https://github.com/erniecohen/dafny/issues/481))

`HigherOrderHeapAllocationChecker` rejects storing a value whose static type contains a function
with read effects (`~>`) in a mutable field or an array element.  In generic code that static type
is a type parameter, so a generic class could store a `() ~> bool` in a field of type `T`, the
stored function could read itself, and a program proved `false`.  The new
`HigherOrderHeapAllocationCheckerGeneric` runs right after it and infers, for each type parameter of
the module, whether its values may be stored in memory: it occurs in the type of a mutable field of
its class, trait or iterator; a store (an assignment or call result into a mutable field, an array
element or a multi-dimensional array element, or an array initializer) has a value type that
mentions it; or it occurs in a type argument passed, anywhere in its scope, to a type parameter that
may be stored (explicit and inferred type arguments, expression types, `extends` clauses, datatype
constructor arguments, newtype and synonym right-hand sides).  Arrays are not stored by fiat: an
array's writes are stores in whatever code makes them.  Instantiating a stored type parameter with
a type that contains a reading function, through datatypes, newtypes and synonyms, is an error that
names the field or store that makes it stored.  The inferred reason is part of the declaration's
interface: the cloner copies it, so it survives export sets and abstract imports, whose signatures
drop bodies, and a `.doo` library is resolved from its source with its bodies.  An overriding
member, or a declaration in a refining module (bodiless trait methods, and classes and other
declarations of abstract modules), may store a type parameter only if the member it overrides, or
the declaration it refines, may.  A base without a body that must allow a store declares its type
parameter with the new characteristic `(!reads)`, spelled like `(!new)`; it is allowed only on a
type parameter, and is the only marker the rule adds.
Its kind is **rejects more**: it adds no fact or axiom, and changes no Boogie program it accepts.
In a resolution screen of the 3,053 other `.dfy` files of the test suite it rejects
no program that was accepted; the only negative test whose output changes is `Landin/Knot8.dfy`,
which gets a second error at the instantiation.  The standard library resolves with no error under
both resolvers.  Still, as for every soundness fix, the hidden `--legacy-generic-function-stores`
option restores the old rule for comparison and migration.  The regression is
`git-issues/github-issue-481.dfy`; the release note is `docs/dev/news/481.fix`.

## Termination of inherited calls in new cycles ([issue 409](https://github.com/erniecohen/dafny/issues/409))

A call between two members of one strongly connected component (SCC) of the call graph gets a
termination check.  A refining module that gives a body to a member without one can close a cycle
through a call it inherits: in the base the callee did not call back, so the base made no check,
and in the refining module the check's origin is inherited, so `Assert` made it an assume.  Lemmas
proved `false`, and compiled methods recursed forever.  The rule follows the design in
https://github.com/erniecohen/dafny/pull/397 (`docs/dev/design/xmod-recursion.md`, section 2.5 and
milestone P2): `BoogieGenerator.InheritedRecursionUnchanged` holds for an SCC when every member has
a counterpart in the refinement base, all in one module, each member's decreases clause is the
counterpart's, copied by the refinement (all its expressions at inherited origins), and every call
between two members is a call between their counterparts in the base's call graph.  The last
condition makes the counterparts strongly connected in the base, so the base placed the inherited
call in one SCC and checked it against the same decreases clauses.  Comparing only the caller and
the callee with the base's SCCs is not enough when a third member joins the cycle.  An inherited
termination check (and the coarse `IsNonRecursive` check) whose SCC fails the test is wrapped in
`ForceCheckOrigin` and asserted, at the base's origin with the refining module as a related
location.  `MustCheckInheritedTermination` decides which copies hold such a call: a member, an
iterator (whose calls are MoveNext's) or a by-method body, copied from the base, recursive in a
changed SCC.  `MustVerifyCopy` and the pipeline schedule them as for issues 404 and 411
(`IsReverifiedInheritedCheck`), with function well-formedness and iterator bodies now marked too,
also when the base comes from an included file or a library; every other assert of a copied body
stays assumed.  A refinement that adds no body to a recursive SCC gets the same Boogie program and
the same tasks as before, with one exception: when the base inferred an empty decreases clause, because
no parameter's type was then decreasable, the refinement infers it again, and a refinement that gives
such a type a definition gets a longer clause.  The SCC then counts as changed, and its intra-SCC
calls are checked against the new clause.  With an empty clause the base can have proved only calls
that need no decrease, such as a function's reference to itself in its own postcondition, so these
checks hold where the base's did.
Its kind is **asserts more**: it rejects programs, by checking an obligation the old rule
assumed, and adds no fact or axiom.  In the test suite it adds three tasks outside its regression,
and all of them verify.  One is in `git-issues/github-issue-404.dfy`, whose module `Decreases` closes a cycle
through an inherited call that decreases, so the new check holds.  Two are in
`cloudmake/CloudMake-ParallelBuilds.dfy`, where `M1.Union` and `M3.GetCert` mention themselves in their
postconditions and their refinements define the opaque `State` and `Path`, which re-infers their empty
decreases clauses.  The hidden `--legacy-inherited-termination-checks` option
restores the old rule for comparison and migration.  The regression is
`git-issues/github-issue-409.dfy`; the release note is `docs/dev/news/409.fix`.

## Functions stored in objects that call back ([issue 453](https://github.com/erniecohen/dafny/issues/453))

`HigherOrderHeapAllocationCheckerConstructor` rejects an assignment to a field whose value's type
has, left of an arrow, the object's type or a trait type, so that a function the object stores
cannot be applied by a function of the object to the object itself: applying a function value gets
no termination check, and the two functions would be a cycle.  A stored function cannot read the
heap (`HigherOrderHeapAllocationChecker`), but it can reach the object through its argument without
reading it: through a datatype constructor's argument, a tuple component, a constant of a class or
trait, or the base of a newtype.  The check looked only at the type and its type arguments, so
`const g: Box -> nat` with `datatype Box = Box(c: C)` proved `false`.  So did a function stored in
another object whose argument reaches that object, a function inside a stored datatype value, and a
class or trait with a permissive (`!T`) type parameter instantiated with the type of the object
that stores the instance.  `HigherOrderHeapAllocationCheckerConstructor.Reach` now follows, left of
an arrow, the arguments of datatype constructors, the constants of classes, traits, datatypes and
newtypes (with those of parent traits), and the bases of newtypes, substituting type arguments.  It
fails at the object's class, with any type arguments, at a trait type, and at an abstract type,
which a refining module or an export may give such a definition.  Elsewhere in the stored value's
type it looks for function values in the same declarations, and it takes the type arguments of a
trait type or an abstract type to be left of an arrow, because an implementation or definition may
store a function of them.  A type parameter is not followed: an instance of a generic class is
checked where it is stored, with the type arguments substituted.  Mutable fields are not followed,
since a function without a `reads` clause cannot read them.  A nested datatype, such as
`N<A> = Leaf(a: A) | Node(n: N<seq<A>>)`, makes a larger instantiation of its declaration at each
step, so the search expands a declaration again, in the same position, only at an instantiation
that is not larger.  At a larger one it searches the type arguments alone, as if left of an arrow;
the expansion in progress searches the rest of the declaration.  So the search ends.  It is
conservative there: a nested datatype instantiated with the object's class, such as `N<C?>` stored
in a field of `C`, is rejected even when it holds no function.  Under `--modular-termination`, a
bounded value ([issue 266](https://github.com/erniecohen/dafny/issues/266)) is exempt as it is in
the old check: its top-level arrow is not searched, but its order type and its function's parameter
and result types are, where the bounded type is.  A cycle through bounded values cannot form.
Applying one is checked to run under a bound strictly below the global bound of the code that
applies it, and its function is checked to run under the value's bound.  So the bounds strictly
decrease along any chain of applications through bounded values, and they are well founded.  In
`github-issue-266-m2.dfy`, `C.f` (global bound 1) applies `cb: bounded<nat, Stored.T -> int>`
with bound 0 to `this`.  The function `cb` holds runs under bound 0, so it cannot call `f`.
Raw functions inside a bounded type are applied without a bound, so a function on a datatype that
holds the object, returned by a bounded function, is still rejected.
Its kind is **weakens only**: it rejects programs and adds no fact or axiom.  It changes no
resolution outside its regression in the test suite, the standard library (its sources,
`modular-termination`, `orders`) or the standard library's examples.  The hidden
`--legacy-stored-function-callbacks` option restores the old check for comparison and migration.
The regression is `git-issues/github-issue-453.dfy`; the release note is `docs/dev/news/453.fix`.

## Opt-in inherited trait axioms ([issue 89](https://github.com/erniecohen/dafny/issues/89))

A bodiless trait lemma marked `{:axiom}` is already an assumption for every caller that holds a
value of the trait type, yet each class that extends the trait had to repeat it, with another
`{:axiom}`.  Under `--inherited-trait-axioms`, default off, a non-static, bodiless, ghost trait
member with `{:axiom}` (a lemma, two-state lemma, ghost method, ghost function or predicate, or
two-state function or predicate; not an extreme one) is a trait axiom, and
`ModuleResolver.ProcessInheritedTraitMembers` no longer asks the classes, datatypes and newtypes
that inherit it to implement it.  The parser records on each trait member whether its file was
parsed under the option (`MemberDecl.DeclaredUnderInheritedTraitAxioms`), and the cloner and the
refinement transformer keep that flag, so the status of a member is fixed where it is declared: a
library built with the option keeps its trait axioms, one built without it has none, and the
option must agree between a library and its client.  Under the option, `{:axiom}` on a bodiless
compiled or extreme trait member is an error unless the member is `{:extern}`.  A refinement
keeps an inherited axiom, may give it a body or strengthen it, and may not add `{:axiom}` to a
bodiless trait member of the module it refines (`RefinementTransformer.KeepTraitAxiomStatus`).
The verifier is unchanged: no axiom, no fact and no obligation is added or removed, since the
trait member's specification was already assumed at every call and an inherited member has no
override check to skip.  `dafny audit` reports each trait axiom, and each type that inherits one
without overriding it.  Its kind is **accepts more, opt-in**: with the option on, programs that
were rejected for a missing implementation are accepted, on the strength of an assumption the
program states and the audit lists; with the option off, the member flag is never set and every
verdict and resource count is as before.  The option does not depend on the resolver, and the
regression runs under both.  The regression is `git-issues/github-issue-89.dfy`; the release note
is `docs/dev/news/89.feat`.

## Opt-in added parents in a refinement ([issue 88](https://github.com/erniecohen/dafny/issues/88))

A refining module could give a type a parent trait only by refining an abstract type into a class;
`trait T ... extends S { }` was a parse error.  Under `--refinement-extends`, default off, the
grammar of a class and of a trait accepts an `extends` clause after `...`, and
`RefinementTransformer.MergeClass` puts the parents of the refined declaration first and the added
ones after them (`TopLevelDecl.ParentTraitsFromRefinementBase` records how many were inherited, so
that the printer, and with it a `.doo` library, writes only the added ones after `...`).  The
merged declaration is then an ordinary declaration of the refining module with those parents, and
every existing rule for an `extends` clause applies to it: cycles, type arguments, one
instantiation per generic parent, cross-module parents, members that a class must implement,
members that may not be redeclared, and the cardinality admission of general traits.  One rule is
new: a refinement may not make a trait a reference type (`ref_added_parent_makes_reference_type`),
because a module that imports the base abstractly may have declared a datatype below the trait.

What makes the feature sound is that the obligations an added parent creates are checked in the
refining module although the text they are checked at is inherited, and those checks are the
fixes of [issue 404](https://github.com/erniecohen/dafny/issues/404) (override checks of members
that become overrides), [issue 411](https://github.com/erniecohen/dafny/issues/411) (definite
assignment of the parent's fields in inherited constructors),
[issue 409](https://github.com/erniecohen/dafny/issues/409) (termination of inherited calls that
the new overrides put on a cycle), [issue 410](https://github.com/erniecohen/dafny/issues/410) (a
member of the parent capturing a name in inherited text) and
[issue 408](https://github.com/erniecohen/dafny/issues/408) (an abstract import bound to a module
that adds a parent).  The change adds no check of its own for them and must not be used without
them: with their `--legacy-*` opt-outs, the regression's `WouldBeBad` methods prove `false`.  The
verifier is unchanged: an added parent yields the axioms that the same parent yields on a new
declaration, and no axiom of the base is removed or weakened.  Its kind is **accepts more,
opt-in**: with the option off the new grammar branch reports a parse error, no merged declaration
changes, and every verdict and resource count is as before.  The option does not depend on the
resolver, and the regression runs under both.  The regression is
`git-issues/github-issue-88.dfy`; the release note is `docs/dev/news/88.feat`.

Two neighbouring cases needed no change to the product, only regressions.  With
`--modular-termination`, the added parent meets the existing rules where a new declaration would:
the bound of an inherited member that becomes an override is compared with the parent's, an
inherited call that the new overrides put on a cycle is checked, and an inherited member that
becomes an implementation of a member of another module that has no bound must be a leaf
(`git-issues/github-issue-88-modular-termination.dfy`, each case beside a one-module control with
the same verdict).  One case is open, and it does not need this option:
[issue 493](https://github.com/erniecohen/dafny/issues/493).  A member that has no bound of its
own and gets one in a refining module, here from the added parent, keeps the calls of its
inherited body unchecked against that bound, and a program proves `false` with it.  The base
line has the same gap where a refinement turns an abstract type into a class that extends a
trait.  Datatypes, codatatypes and newtypes keep the grammar they had,
`datatype D extends S ...`, which needs no option, and the same default-on fixes cover the parents
they add: override checks at inherited members, termination, name capture, the cardinality
admission, and trait axioms under `--inherited-trait-axioms`
(`git-issues/github-issue-88-value-types.dfy`).  The legacy resolver has no conversion from a
datatype or a newtype to a trait, so the cases that upcast run under the new resolver only.  Not
covered: member invariants, whose option is not in this line yet.

## Comparing crossed component types ([issue 47](https://github.com/erniecohen/dafny/issues/47))

With `--type-system-refresh`, `==`, `!=`, `in`, `!in` and prefix equality required, in their final
check, that one operand's type be a supertype of the other's, so `(Number, Integer) == (Integer,
Number)`, with `Number` and `Integer` subtypes of one trait, was rejected, though inference had
already found the two comparable and the legacy resolver accepts it.  The check now also accepts
two types with the same type constructor whose arguments agree exactly at each non-variant
parameter and are comparable, recursively, at each co- or contravariant one.  Such types have a
common supertype and a common subtype, built argument by argument, so a value can belong to both
and the comparison is meaningful.  It is a **false-rejection fix** of the refreshed resolver: it
accepts more programs and adds no fact or axiom; the legacy resolver and `as`/`is` are unchanged.
The regression is `git-issues/github-issue-47.dfy`, with negative controls that are still
rejected; the release note is `docs/dev/news/47.fix`.

## Match inference on by default ([issue 48](https://github.com/erniecohen/dafny/issues/48))

With `--type-system-refresh`, resolving a pattern whose type was not yet known solved the type
constraints with default decisions, which gave a pending literal elsewhere in the enclosing
expression, such as the `0` of an earlier `if x == 0 then Some(0)`, the type `int` before the
declared result type `Option<int32>` could give it `int32`.  `--conservative-match-inference`,
which propagated without decisions first, fixed this for a match source of unknown type, and is
now **on by default**; `--conservative-match-inference:false` restores the earlier inference.  It
also now decides without default decisions that an identifier without arguments in a pattern is a
bound variable when, after propagation, its type has a subtype that is not a datatype, or advice
that only a number, `bool` or `char` type satisfies; a datatype has no subtypes but itself, so the
identifier cannot be a constructor.  That fixes `var d := Some(0); match d case Some(n) => ...`
too.  It is a **false-rejection fix** of the refreshed resolver: it adds and removes no type
constraint, only defers default decisions, so a literal's newtype constraint is still checked by
the verifier, and a value already of type `int`, of another newtype or of `bool` is still
rejected.  The legacy resolver ignores the option.  When a pattern does not match its source, the
default decisions are made before the error is reported, so that the message names the types
they give.  The regression is `git-issues/github-issue-48.dfy`; the release note is
`docs/dev/news/conservative-match-inference-default.fix`.

## Printing the resolved program ([issue 441](https://github.com/erniecohen/dafny/issues/441))

Four printing tests had fallen behind the fork.  Three are printer output that the fork changed on
purpose, so their `.expect` files are updated.  The comment `// bitvector types in use` lists the
widths that the translator declares: in increasing order, and including the bit length of a width
that a shift lowers to (`bv67` adds `bv7`).  `git-issues/git-issue-1665.dfy` prints the
module-qualified names generated for `:-` and gives an untyped case variable the type of the match
source, `nat`.  One was a bug: under `--type-system-refresh`, the published `_System` lost each
`bvN` declaration with its `RotateLeft` and `RotateRight`, because the dependency-local translation
inputs recorded a bitvector type's width but not its declaration, so `--rprint` no longer listed
them.  `ModuleTranslationInputs` now records the name too.  `dafny0/BitvectorsMore.dfy.refresh.expect`
had been rewritten to the broken output when dependency-local translation became the default; it
now lists the declarations again, as 4.11.0 did.  Its kind is **no effect on
verification**: the translator does nothing for these declarations, the solver input of each of the
four tests is byte-identical with and without the change, under both resolvers, and so is compiled
output.  The four tests are now in `.github/review/regressions`; the release note is
`docs/dev/news/441.fix`.  The fifth test of the issue, `dafny0/Superposition.legacy.dfy`, is left
to [issue 436](https://github.com/erniecohen/dafny/issues/436).

## Yield parameters checked after grounding constructors ([issue 445](https://github.com/erniecohen/dafny/issues/445))

The resolver checked that the type of each yield parameter supports auto-initialization in
`ResolveIteratorSignature`, while `ResolveTopLevelDecls_Signatures` was still resolving the module's
declarations in source order.  No datatype had a grounding constructor yet, so `Type.GetAutoInit`
followed its fallback, the first ghost constructor or else the first constructor, and the
constructors of a datatype declared after the iterator, or inherited by a refining module, still had
unresolved types.  So a recursive first constructor made a datatype with values look empty, a false
rejection or, before [issue 413](https://github.com/erniecohen/dafny/issues/413), a stack overflow;
an unresolved type crashed the resolver; and a first constructor with a compilable value hid a
grounding constructor without one, as in `datatype D<T> = C1(t: T) | C2(s: S)` with `S` declared
`witness *`.  The compiler initializes a yield parameter with the default value of its type, made
with the grounding constructor, so the compiled iterator left such a parameter null, and a verified
program printed `null` or threw a `NullReferenceException`.  The check now runs in
`ResolveTopLevelDecls_Core`, right after `SccStratosphereCheck` has found the grounding
constructors, so it agrees with the compiler and with the checks of class fields and of `(0)` and
`(00)` type arguments.  Ghost yield parameters are checked the same way, as ghost fields are, and,
like the other checks in that block, the check is skipped when the module already has errors.  Its
kind is a **crash and false-rejection fix**, and, for the null values, a soundness fix that **weakens
only**: it rejects programs whose compiled iterator could read a value it never initialized, accepts
only programs whose yield types have a default value the compiler makes, and adds no fact or axiom.
The hidden `--legacy-early-yield-parameter-check` option restores the old check at its old place,
crashes included, for comparison and migration.  The regression is
`git-issues/github-issue-445.dfy`; the release note is `docs/dev/news/445.fix`.

## Functions stored through every left-hand side ([issue 483](https://github.com/erniecohen/dafny/issues/483))

`HigherOrderHeapAllocationChecker` rejects storing a value whose type contains a function with
read effects (`~>`) in memory, so that such a function cannot read itself.  It looked only at a
`SingleAssignStmt` whose left-hand side is a `MemberSelectExpr` (a field) or a `SeqSelectExpr` (a
one-dimensional array element).  A multi-dimensional array element (`MultiSelectExpr`) is memory
too, and a method call (`CallStmt`) stores its out-parameters in its left-hand sides, which may be
fields, array elements or an iterator's yield parameters.  Through either, a function could read
itself, and a program proved `false` under both resolvers.  The checker now treats a
`MultiSelectExpr` left-hand side like the other two, and checks each left-hand side of a call that
is a memory location against the type of the corresponding out-parameter, with the call's type
arguments substituted.  Like the plain assignment, which looks at the type of its right-hand side,
the call is rejected only if the value it stores may contain a reading function, so storing a
`Pack<() -> bool>` result in a `Pack<() ~> bool>` element is still accepted.  The other ways to
write memory were already covered: a multi-assignment, a `forall` statement and `:-` resolve to
`SingleAssignStmt`s; an array initializer makes an array whose type contains `~>`, and storing that
array is rejected; `:|` supports only local variables; and a constructor's first phase cannot
assign a `const` field through a call.
Its kind is **rejects more**: it adds no fact or axiom and changes no Boogie program it accepts.
In a resolution screen of the other 3,056 `.dfy` files of the test suite, no file changes its exit
code or its errors, and the standard library resolves with no error under both resolvers.
Still, as for every soundness fix, the hidden `--legacy-function-store-locations` option restores
the old check for comparison and migration.  The regression is `git-issues/github-issue-483.dfy`;
the release note is `docs/dev/news/483.fix`.

## Abstract imports bound to non-refinements ([issue 408](https://github.com/erniecohen/dafny/issues/408))

A module `C refines B` may say which module stands for `B`'s abstract import `import X : A`,
and the language requires that module to refine `A`: `B`'s proofs were made from `A`'s contracts,
and `C` inherits them as assumptions.  `RefinementTransformer.PreResolve` accepted any module
declaration in that place without a check (`CheckIsRefinement` is reached only for declarations
written with refining notation, which an import never is), so `import X = Other`, where `Other`
merely has members with `A`'s names, proved `false`.  The transformer now requires the module that
the new declaration denotes to be `A` or to reach `A` along its chain of `refines` clauses.  An
alias is followed to the declaration it names, and an abstract import, including an alias of
another module's abstract import, stands for its original target, not for its signature facade.
The check runs only where an inherited abstract import is overridden, after the overriding
declaration has been resolved.  Its kind is **weakens only**: it rejects programs and adds no
fact or axiom; a program whose bindings refine their targets is unchanged.  The hidden
`--legacy-unchecked-import-binding` option restores the old rule for comparison and migration.
It does not check that an export set of the binding reveals what the import's own export set
does; the import's facade is resolved from the target's signature, and a binding through a
narrower export set is caught where an inherited member is missing.  The regression is
`git-issues/github-issue-408.dfy`; the release note is `docs/dev/news/408.fix`.
The rule is nominal, as in the design drafts of [PR 401](https://github.com/erniecohen/dafny/pull/401)
and [PR 397](https://github.com/erniecohen/dafny/pull/397).  The reference manual had said the
binding must "adhere" to the target, and its example bound `import A = Implementation` where
`Implementation` did not declare `refines`; no structural check of that kind ever existed.  The
change updates `docs/DafnyRef/Modules.md` §4.6, `docs/DafnyRef/Refinement.md` §10.2,
`docs/HowToFAQ/FAQImportAbstractModule.md` and `docs/HowToFAQ/onepage.md` to the nominal rule
and adds `refines Interface` to the example; `docs/OnlineTutorial/Modules.md` already had it.

## Names captured in inherited code ([issue 410](https://github.com/erniecohen/dafny/issues/410))

A module `R refines B` gets clones of `B`'s declarations and resolves them again in its own
scope.  A declaration that `R` adds, or a module that `R` binds to an abstract import, could
capture a name that the inherited text used: a function or type with the name of one from an
opened import, a nested module, a class member hiding a module-level constant, a member of an
added parent trait, a local variable of a statement skeleton, or a constant or constructor that
turns a pattern variable into a pattern.  The inherited proofs are assumed in `R`, so they then
held of the new meaning, and `R` proved `false`.  The fix is the exact resolution-stability check
that section 4.3 of the refinement design draft
([pull request 401](https://github.com/erniecohen/dafny/pull/401)) defines and its open question
2 recommends.  The refinement cloner records each inherited occurrence of a name (an identifier,
a qualified name, a type name, a pattern identifier) with the node it was cloned from, and after
`R` is resolved, `RefinementCaptureCheck` requires each one to denote the counterpart of what it
denoted in `B`: the clone or refinement of a declaration of `B` or of a module nested in it; the
clone of a local binder, or the local of `R` that a statement skeleton merged with it; the same
declaration through an import that `R` keeps; the declaration of the same name in the module
bound to an abstract import, which must refine its target; a member of the same name in the
counterpart of its type, or a member that overrides it, including a member of a type synonym
that refines an abstract type (`--synonym-refines-abstract-type`).  A pattern identifier keeps its
kind.  A default `decreases` clause needs no check: `R` inherits the clause that `B` inferred, so
defining a parameter's abstract type does not change it.  The error names the captured name and
both declarations.  Its kind is **weakens only**: it rejects programs and adds no fact or axiom;
the check only reads the resolved program, so an accepted program gets the same solver input.  It
rejects one program of the suite, `exports/OpenImportRefined.dfy`, whose `RefinedF` proved
`false` through a captured `f`; its harmless twin `Refined`, where `type T = int` captures the
`T` of an opened import that is also `int`, is rejected too, because the rule compares
declarations, not their expansions.  The hidden `--legacy-inherited-name-capture` option restores
the old rule for comparison and migration.  The regression is `git-issues/github-issue-410.dfy`;
the release note is `docs/dev/news/410.fix`.

## Renaming in a refinement ([issue 106](https://github.com/erniecohen/dafny/issues/106), [issue 107](https://github.com/erniecohen/dafny/issues/107))

`--refinement-renaming` is an opt-in feature: stages 1 to 3 of the refinement design draft
([pull request 401](https://github.com/erniecohen/dafny/pull/401), sections 2.3 to 2.5).  A
declaration of a refining module `R` may rename the type parameters, the out-parameters, the
result and the local names of the in-parameters of the declaration of `B` that it refines, by
position.  With the option off, the name-agreement errors are those of 4.11.0, and nothing else in
this change is reached.

*Labels.*  A `Formal` has a `Label`, the name a named argument uses.  It is the parameter's name
in the first declaration of the chain, and both resolvers bind named arguments by it, so no call,
inherited or not, is affected by a renaming.  The label is derived where a formal is copied under
a different name, and so follows every later copy.

*The renaming.*  The refinement cloner copies `B`'s declaration under a map from `B`'s binders to
their new names.  It renames by what `B` resolved each name to, not by spelling: a name segment
or type name that `B` resolved to a renamed binder gets the new name, and no other occurrence is
touched.  The copy is then resolved in `R` like any inherited code.

*Why it is sound.*  The copy must mean what `B`'s text meant.  The check of
[issue 410](https://github.com/erniecohen/dafny/issues/410) already requires every inherited
occurrence to denote the counterpart of what it denoted in `B`; three clauses extend it to
renamed binders.
(1) The counterpart of a renamed binder of `B` is exactly its renamed copy in `R`, and a renamed
copy is the counterpart of nothing else.  This rejects a new name that an inner binder of the
inherited code captures, and a new name that captures an inherited use of another variable,
constant or type.  (Without a renaming, 410 accepts any parameter of the same name, which is not
enough here.)
(2) A renamed occurrence that `R`'s resolver does not resolve itself (because a desugaring
resolves a copy instead) is an error, where 410 skips an unresolved occurrence.
(3) An inherited occurrence that `B` left unresolved and that spells an old or a new name of a
renaming in force is an error, since the map cannot tell what it refers to.
Since the argument rests on the 410 check, the option is rejected together with
`--legacy-inherited-name-capture`.  Its kind is **weakens only** for the verifier: it adds no fact
or axiom; under the option, a renamed declaration is verified as the alpha-variant that the
cloner wrote.

*Signature agreement.*  Counts, modifiers and type characteristics are compared as before.  The
types of the parameters and the result type are first compared as text, after `B`'s side is put in
`R`'s names, as without the option.  Under the option, a type that is spelled differently is not
an error yet: the refinement transformer hands the spelling to the merged declaration
(`MethodOrFunction.RefinementDeclaredTypes`).  The resolver resolves it in the scope of the
merged signature, and once all signatures of `R` are resolved it compares the spelling with the
type the merged declaration has, by `Type.Equals` with constraints kept: type synonyms are
expanded, a subset type or newtype equals only itself, and so does a type that `R` sees as
opaque.  A mismatch is the error of the textual comparison, reported before any body is resolved.
The comparison is not part of the soundness argument.  The merged declaration takes every
parameter and result type from `B`, also where `R` names a result that `B` leaves unnamed, so
`R`'s spelling occurs nowhere in the resolved program and the comparison decides only which
spellings are accepted.  Type-parameter bounds and the types of constants and fields are still
compared as text: a refining `const` keeps its own declaration, so for it the comparison would
decide which type the program has.

*Not renamable.*  An iterator's signature can only be repeated with `...`.  The type parameters of
an abstract type with members that a type synonym refines (`--synonym-refines-abstract-type`) keep
their names.  A label has no syntax of its own: a refining declaration cannot rename it, and the
printer prints local names.

*Three copies made once.*  A field and an array allocation each cloned their explicit type twice,
for a syntactic and a working slot that the parser fills with one object; the resolver resolves
one, so the other copy could not be checked by clause (2).  The copy constructors now keep them
one object, as the parser does.  A `match` pattern's type annotation was not cloned at all: the
copy shared the type object that `B` had resolved, so a pattern inherited by `R` kept a type of
`B`, and the resolver crashed or reported a mismatch whenever that type is a type parameter or a
type that `R` refines.  It is now cloned and resolved in `R`.  This last change is not behind the
option; it turns a crash or a spurious error into an accepted program.

The regression is `git-issues/github-issue-106.dfy` with its inputs; the release note is
`docs/dev/news/106.feat`; the manual's section is "Renaming parameters and type parameters".

## Copying imports ([issue 107](https://github.com/erniecohen/dafny/issues/107))

`--copy-imports` is an opt-in feature: stage 4 of the refinement design draft
([pull request 401](https://github.com/erniecohen/dafny/pull/401), sections 3.2, 3.3 and 3.5).  It adds one
declaration, `import [opened] X := Q` with an optional export suffix, which gives the importing
module `M` a copy of `Q` named `X`.  With the option off, the parser does not accept the
declaration, and nothing else in this change is reached.

*What a copy is.*  The parser builds a nested module `X` of `M` that refines `Q` and has no
declarations of its own, marked as a copying import.  Everything after that is the existing
machinery of refinement: the refinement transformer clones `Q`'s declarations into `X`, and the
resolver, the translator and the compilers see an ordinary module.  So the types of `X` are new
types, and its functions get the bodies of `Q`, by the rules that already hold for
`module X refines Q { }`.  No axiom is added, and a program without the declaration gets the
same solver input.

*What differs from a written-out refinement.*
(1) The default export set of `Q` is the default export set of the copy and takes the copy's
name, also where another export set extends it.  (A written-out refinement keeps it as a
non-default set called `Q`.)  `Q` may therefore not have another export set with the copy's name.
(2) The name `X` gives the default export set, or the sets named after the backquote; the
signature it gives is kept on the declaration and is used for `X.a`, for an alias `import A = M.X`
and for an export of `M` that provides `X`.
(3) A copy of an abstract module is abstract, and a compiled module may not have one.
(4) An `{:extern}` declaration of `Q`, or `Q` being `replaceable`, is an error: the first would
give two Dafny declarations for one thing of the target program, and the second is already an
error for a written-out refinement.

*Copies in a refining module.*  When `M'` refines `M`, the transformer does not clone `M`'s copy
`X` (which would give `M'` the types of `M.X`) but declares a new copying import of the same
module, with the same export suffix.  That module is not a declaration of the program's module
graph, so `M'` resolves it itself, before its own declarations, and hands its signature to the
program with its own.  A redeclaration of `X` in `M'` is an error.  The same holds for each
further refinement.

*What is deliberately unknown.*  An abstract import `import A : B` inside `Q` stands for an
unknown refinement of `B`.  Each copy gets a signature of its own for it, so nothing relates
`X.A` to `Q.A` or to `Z.A`.  Through `import I : M`, the copy `I.X` is likewise an abstract
signature: `I` may be any refinement of `M`, each of which has copies of its own, so the types of
`I.X` are known neither to be those of `M.X` nor to differ from them.  The controls of the
regression test fail in both directions.

*Binding an abstract import.*  A copying import in a refining module may take the place of an
abstract import `import A : B` that the module inherits, as a nested module may.  The rule of
https://github.com/erniecohen/dafny/issues/408 applies to it unchanged: the copy refines the
module it copies, so `import A := R` is accepted when `R` is `B` or refines it, and is otherwise
the error `a module (A) can only be replaced by a refinement of the original module`.  The
regression test has both cases.

*Proof cost.*  The verifier does not verify the members that a refining module inherits
unchanged, so a copy adds no proof, with one exception.  The checks that a refining module makes
again for an inherited member are those of https://github.com/erniecohen/dafny/issues/404 (an
override whose overridden member is not the one the refined module checked),
https://github.com/erniecohen/dafny/issues/411 (a constructor of a class to which the module
added a field) and https://github.com/erniecohen/dafny/issues/409 (the termination of a group of
recursive members that the module changed).  A copy has no declaration of its own, so the last
two do not apply to it, and the first applies in one case: `Q` has a copying import `Y`, and a
class of `Q` extends a trait of `Y`.  In a copy `X` of `Q` the trait belongs to the new copy
`X.Y`, which is not the module that `Q` checked the class against, so the check of each override
is made again, once for each copy of `Q`.  The same check is made for a written-out
`module X refines Q { }`.  It is redundant, because `X.Y` and `Q.Y` copy the same module, and it
is kept because the rule that makes it is the simple one.  The regression test has six copies of
a module with a lemma, six copies of a module with an override, a constructor and a recursive
function, which add no proof, and four copies of a module of the excepted kind, which add one
proof each.

*Libraries.*  The option is recorded in a library, and a library built with it is loaded only
under it, with the error `--copy-imports is set locally to False, but the library was built with
True`.  The text of a library that uses the declaration does not parse without the option.

The regression is `git-issues/github-issue-107.dfy` with its inputs; the release note is
`docs/dev/news/107.feat`; the manual's section is "Copying imports".
