# Plan: sound termination for traits extended across modules (issue 211)

Issue: https://github.com/erniecohen/dafny/issues/211 (upstream dafny-lang/dafny#1588).
Related: https://github.com/erniecohen/dafny/issues/245 (well-founded orders) and
https://github.com/erniecohen/dafny/issues/266 (bounded callables).
Base: `review/4.11.0` at `5a7603b60`.

This plan collects the work so far into one design and one rollout. It does not
replace the detailed designs it builds on:

- [`design/211-m4b-library-openness.md`](design/211-m4b-library-openness.md): declared openness, termination
  effects, `.doo` records, and the soundness argument (its §7);
- [`design/245-orders-in-bounds.md`](design/245-orders-in-bounds.md): orders in bounds;
- [`design/266-bounded-callables.md`](design/266-bounded-callables.md): stored and returned callbacks;
- Reference Manual §7.1.3.2 and §7.1.3.3 (`docs/DafnyRef/Specifications.md`).

**The owner's decision (revision 2).** "Since we are presumably getting rid of all
of our uses of termination false, it seems to make little sense to break legacy
uses." So the checked design gets a new declaration, `@Extensible`. The legacy
forms, `{:termination false}` and `@AssumeCrossModuleTermination`, keep their 4.11
meaning. No default changes, and no legacy program changes verdict.

## 1. The problem

A class, datatype or trait may extend a trait from another module only if that trait
has `{:termination false}` or the extending type has `@AssumeCrossModuleTermination`
(`ModuleResolver.cs`). Dafny then does not check calls dispatched through the trait
for termination. Dafny's termination metric is really `(module, SCC, decreases)`
(Leino, dafny-lang/dafny#1588). The first two components are left out because,
without cross-module dispatch, an import can never lead back up. A foreign
implementation breaks that: it can call back into the module that dispatched to it.

fabiomadge's "Tie" program (dafny-lang/dafny#1588, 2024-02-26) shows that no
call-graph summary of one module can stop this. Here it is with one `+ 1` added, so
that the cycle proves `false`:

```dafny
module Tr { trait {:termination false} T { function A(other: T): int  function B(other: T): int } }
module X { import Tr  class X extends Tr.T { constructor () { }
  function A(other: Tr.T): int { 0 }
  function B(other: Tr.T): int { other.A(this) } } }
module L { import Tr  function Tie(t1: Tr.T, t2: Tr.T): int { t1.B(t2) } }
module Y { import Tr  class Y extends Tr.T { constructor () { }
  function A(other: Tr.T): int { other.B(this) + 1 }
  function B(other: Tr.T): int { 1 } } }
module M { import Tr import X import Y import L
  method Main() {
    var x: Tr.T := new X.X();  var y: Tr.T := new Y.Y();
    assert L.Tie(x, y) == y.A(x) == L.Tie(x, y) + 1;
    assert false;
  } }
```

`L` imports neither `X` nor `Y`. Each module, read alone, has no cycle. The cycle
`X.B -> Tr.T.A -> Y.A -> Tr.T.B -> X.B` exists only in the program as a whole.

On release `v4.11.0+fcb2042d.review.6f900967` with Z3 5.1.0:

| Program | default options | `--modular-termination` |
|---|---|---|
| Tie, as above | verifies, `assert false` included | rejected: 4 dispatched calls to members without a bound |
| Tie with `decreases {:global} n` on `A` and `B`; each override calls with `n - 1` | rejected (`{:global}` is read as an ordinary decreases clause) | verifies |
| the same, but `Y.A` calls `other.B(this, n)` | rejected | rejected at that call: the callee's bound must be below the caller's |
| the same, but `X.B` calls the helper `L.Tie(other, this, n)`, which has no bound | rejected | rejected at the helper call: same-bound rule |

Since https://github.com/erniecohen/dafny/pull/386, a default `dafny verify`
reports the assumption. On the Tie program it warns that trait `T` has
`{:termination false}` and no `{:axiom}`, and exits 2 unless `--allow-warnings` is
given. What is missing is a way to write a trait that other modules extend and have
Dafny check it. That is this plan.

## 2. The design: `@Extensible`, with bounds in interfaces

**Chosen design.** A trait whose own declaration carries `@Extensible` may be
extended from other modules, and calls dispatched through it are checked. A member
that other modules may implement gets a *specification-level outer bound*, a value
in a well-founded order, written `decreases {:global} E`. Every implementation is
verified once, in its own module, against that interface. This is the sequential
level discipline of Jacobs, Bosnacki and Kuiper ("Modular Termination
Verification", ECOOP 2015; TOPLAS 2018), without call permissions.

This is what `--modular-termination` implements today (milestones M1 to M5b on
issue 211), where `{:termination false}` is read as "extensible, checked". The
revision moves that meaning to `@Extensible`, and makes the checks follow the
declaration instead of an option. A program with no `@Extensible` trait, and none of
the annotations of §2.4, is treated exactly as in 4.11.

The rules, with `B` the caller's bound captured on entry, `b` the callee's bound
evaluated at the call, `<` and `<=` the strict and weak relations of the bound's
order, and TOP above every proper bound:

1. **Openness is declared by the trait's module**, with `@Extensible` on the trait's
   own declaration. A client cannot open a trait that does not declare it. For the
   legacy forms, see §2.3.
2. **Dispatched call** through an `@Extensible` trait: `b < B`. The callee needs a
   proper bound, or gets BOTTOM by §2.2. A call from TOP code, which has no bound,
   holds automatically.
3. **Known call** to code that can reach checked dispatch (`General(b)`): `b <= B`,
   plus Dafny's existing `(module, SCC, decreases)` check. A known call to code that
   cannot reach any open dispatch (`Closed`, `{:termination_closed}` across imports)
   needs only the existing check.
4. **Overrides** inherit the bound. An explicit one must be `<=` every inherited
   bound. Dispatch to the implementation itself is not a second strict step.
5. **Bounds are closed** (M4a): a bound, and the order it uses, may not reach open
   dispatch. Its value cannot depend on the code whose termination it orders.
6. **Function values** (M3, issue 266): applying a function value whose code is
   not known is TOP, so it is rejected in bounded code. A callback parameter with
   `decreases {:callback f} E`, or a value of type `bounded<O, F>`, carries checked
   evidence that its code runs under the bound `E`. Evidence is created only where
   it is checked, never by extensional equality, and it can be copied freely.
7. **Orders** (issue 245, issue 264): any order with a well-foundedness proof
   (`decreases {:global} e by O`). The weak and strict relations must satisfy
   `a <= b < c  ==>  a < c`. The repaired tuple comparison of issue 264 does.
8. **Libraries** (M3-4, M5): a library that extends or dispatches through an
   `@Extensible` trait is admitted only with a termination record, written by a
   build that verified it. The record holds the library's contracts, and clients
   depend only on those, never on its bodies. §5 covers libraries without one.
9. **Legacy dispatch is unknown code.** A call dispatched through a trait that is
   open only by a legacy form is treated like the application of an unknown
   function value: TOP. Bounded code and `Closed` code cannot make it, directly or
   through a known call (rules 3 and 6). Code with no bound may make it, and that
   call is the legacy premise, warned at its declaration.

### 2.1 Why it is sound

Take any infinite chain of nested calls in an accepted program whose legacy premises
hold. Rank each activation by the pair (outer bound, existing static metric),
compared lexicographically.

- A dispatched call through an `@Extensible` trait, or a callback application,
  strictly lowers the outer bound (rules 2 and 6).
- A known call keeps the outer bound or lowers it (rule 3). When it keeps it,
  Dafny's existing `(module, SCC, decreases)` check applies. In a stretch of calls
  that keep the bound, every call is a known call, so the call graph is the one
  Dafny already reasons about inside one program, and the existing check orders it.
- An override enters the implementation at or below the slot's bound (rule 4).
- A legacy dispatched call happens only at TOP (rule 9). So a chain that makes
  infinitely many of them never leaves TOP, and it is exactly the kind of chain the
  legacy premise excludes. Bounded code cannot reach legacy dispatch at all, so
  every checked guarantee holds whatever the legacy premises say.

The strict decreases are all in one well-founded order (rule 7), so there are
finitely many. After the last one, every call keeps the bound, and the existing
discipline rules out an infinite tail. Rule 5 makes the bound a value fixed before
the calls it orders, so the inequalities are proved without assuming the
functions whose termination they establish. The M4a counterexample (e8 on issue
211) is what happens without this rule. Rule 8 makes the argument per module.
Each implementation's obligations mention only the interface, so a body that
another module adds later cannot invalidate them. M4b §7 has the full argument,
including refinement, export views and abstract modules. Replacing "extensible
trait" there by "`@Extensible` trait", and adding rule 9 as one more source of TOP
next to unknown function values, does not change the argument.

**Tie.** With `@Extensible` in place of `{:termination false}`, `A` and `B` are
declared in `Tr`, so their bounds `bA(this, other)` and `bB(this, other)` are fixed
in `Tr`. Module `X` must prove `bA(other, this) < bB(this, other)` for every
`this: X`. Module `Y` must prove `bB(other, this) < bA(this, other)` for every
`this: Y`. With `this = x` and `other = y`, these say `bA(y, x) < bB(x, y)` and
`bB(x, y) < bA(y, x)`. That is impossible in a strict order. So one of the two
modules fails to verify, in that module, without knowing the other exists. The
helper `L.Tie` cannot hide the cycle. If it has no bound, it is TOP and bounded code
cannot call it. If it has a bound `bT`, `X.B` must show `bT <= bB` and `L.Tie` must
show `bB < bT`. The run above shows each of these cases. Without bounds, `A` and `B`
get BOTTOM (§2.2), and both bodies are rejected, since a BOTTOM implementation can
make no dispatched call.

### 2.2 One addition: an extensible member without a bound is a leaf

Today an extensible member with no bound has none at all. Any dispatched call to it
is rejected, even from `Main` (the first row of the table). That forces a bound on
every member of every extensible trait, including the common
`function value(): nat { n }`.

Rule 2.2: **a member of an `@Extensible` trait with no declared or inherited global
bound has the bound BOTTOM**, a proper bound adjoined below every other one.

- A dispatched call to it holds from any caller, since BOTTOM is below every bound
  and below TOP.
- Its implementations run under BOTTOM. They can make no dispatched call and apply
  no callback, since nothing is below BOTTOM. They can call `Closed` code, and
  known code whose bound is BOTTOM.
- BOTTOM is the least bound in the order. Under rule 4, an override cannot raise it.

This is sound: BOTTOM is just a particular bound, and every rule above still
applies. It only accepts programs that are rejected today: dispatch from code with
no bound, to implementations that are leaves. Its cost: the order of bounds gets a
least element next to TOP. That is one more case in `ConsistentDecreasesCheck`,
which already handles TOP.

### 2.3 Legacy forms, and programs that mix them

`{:termination false}` and `@AssumeCrossModuleTermination` keep their 4.11 meaning:
an unchecked premise that dispatch through the trait terminates. As merged in
https://github.com/erniecohen/dafny/pull/386, `dafny verify` warns about each one by
default (hidden opt-out `--legacy-no-unchecked-termination-warning`), `dafny audit`
reports it, and `{:axiom}` on the declaration, or `--allow-axioms`, acknowledges it.
`@Extensible` is never warned about or audited, since it assumes nothing.

When a program mixes the forms:

| Situation | Verdict | Reason |
|---|---|---|
| One trait with `@Extensible` and `{:termination false}` | error | The two say opposite things about the same dispatch: checked and assumed. |
| A type with `@AssumeCrossModuleTermination` that extends a foreign `@Extensible` trait | error | The extension is already allowed. The attribute could only mean "skip the checks", which would let one client weaken a guarantee the trait's author made. |
| A trait with `{:termination false}` that has an `@Extensible` ancestor | error: write `@Extensible` | See below. |
| An `@Extensible` trait with a legacy-open ancestor | allowed | Members declared in the ancestor keep the legacy meaning (rule 9). Members declared in the `@Extensible` trait are checked. |
| A type with `@AssumeCrossModuleTermination` that extends both a foreign sealed trait and a foreign `@Extensible` trait | allowed | The attribute covers the sealed parent only. The overrides of `@Extensible` slots are checked as usual. |
| Bounded code that reaches a legacy dispatched call | error | Rule 9. |

**The subtrait case.** A subtrait `S` of an `@Extensible` trait `E` that is itself
open to other modules exposes `E`'s slots to more implementations. If `S` could be
opened with `{:termination false}`, those implementations would sit behind a legacy
premise in `S`'s module, which `E`'s author cannot see, and `E`'s guarantee would
depend on it. Checking them anyway, against `E`'s bounds, would give
`{:termination false}` a second meaning that depends on an ancestor. The error
avoids both. It breaks nothing: no existing program has an `@Extensible` ancestor,
and the fix is to write `@Extensible` on `S`. The same reasoning gives the error for
`@AssumeCrossModuleTermination` above.

### 2.4 When the checks run

The checks are opt-in by use, as `AGENTS.md` requires of a new feature. They apply
to a program when it has an `@Extensible` trait, a `{:global}` or `{:callback}`
bound, a `{:termination_closed}` promise or a `bounded<O, F>` type, in its own code
or in a library's record. Without any of these, the program goes through the 4.11
code paths, with identical Boogie output.

Two consequences:

- `decreases {:global} E` gets its meaning without an option, so the second row of
  the table verifies by default once `T` says `@Extensible`. Today, without the
  option, `{:global}` is read as an ordinary decreases clause. The only programs
  that rely on that are fork tests of the option, which stage 3 migrates.
- Legacy code with no bound is never given a new obligation. Rules 2 and 3 hold
  automatically from TOP, rule 9 only restricts bounded code, and only new code is
  bounded or implements an `@Extensible` slot.

`@Extensible` adds checks where the legacy forms assumed. A program using it is
never less checked than the same program with `{:termination false}`. So it can ship
before stage 3, marked experimental in the Reference Manual, with the remaining
gaps of §5 stage 2 listed there.

### 2.5 Why not the other proposals

| Proposal (dafny-lang/dafny#1588) | Soundness | Modularity | Burden |
|---|---|---|---|
| Cycle check on module signatures (keyboardDrummer) | Rejects Tie | No. The error appears in `M`, the module that imports both `X` and `Y`, and it depends on which implementations are in scope there. A library cannot be verified once. | No annotations, but rejects every terminating cross-module cycle, such as the counter version of Tie. |
| `calls` clauses (keyboardDrummer) | Defeated by Tie | — | — |
| Required `decreases` on bodiless trait members (Mayer) | Sound only if those decreases are compared across modules. Then they are the global bounds of this design under another name, and they also need rules 3 and 5. | Yes, once made precise | About the same as this design without §2.2 |
| Module and SCC components in decreases, with exported ordering edges (Leino) | Sound | Partial: modules publish their internal SCC order | Heavy: names of SCCs in specifications |
| This design | Sound (§2.1) | Yes: clients depend on contracts only (M4b §8) | A bound only on members whose implementations dispatch or call back |

The choice rests on two principles. Termination of dispatch has to be a property
of the interface, since the implementation is unknown. And a client may depend
only on what a library publishes. Of the proposals above, only this one has both.

## 3. What user code must change

Nothing, to keep verifying as in 4.11. The legacy forms keep their meaning and their
default warning.

To make a trait checked:

1. **Replace `{:termination false}` by `@Extensible`** on a trait that other
   modules extend, and drop `@AssumeCrossModuleTermination` from its extenders.
   A trait with neither can still be extended only in its own module.
2. **Members whose implementations are leaves** need nothing (§2.2).
3. **Members whose implementations make dispatched calls or apply callbacks** need
   `decreases {:global} E`, and each implementation must prove the strict
   decrease (rule 2). The order may be any order of issue 245.
4. **Helpers that forward a dispatched call from bounded code** need a bound of
   their own, at most the caller's, or `{:termination_closed}` if they cannot reach
   dispatch. Dafny does not infer helper bounds. If every dispatched member had a
   bound, the projection from M3 was 185 helpers in the standard library.
5. **Callbacks** need `decreases {:callback f} E` on the parameter, or the type
   `bounded<O, F>` if the value is stored or returned (issue 266).
6. **A bound may not be computed through dispatch**, so `decreases {:global}
   t.Size()` with a trait member `Size` is rejected (rule 5).
7. **Bounded code may not call through a legacy-open trait** (rule 9). Make that
   trait `@Extensible` too, or keep the caller unbounded.
8. **Libraries** that extend or dispatch through `@Extensible` traits are rebuilt
   as `.doo` files with a termination record (rule 8).

## 4. Implementation sketch

Most of the design is already in the code, behind `--modular-termination`.

| Piece | Files | State |
|---|---|---|
| `@Extensible` attribute | `AST/Attributes.cs` (built-in, traits only), `Resolver/ModularTermination.cs` (`IsExtensible`), `ModuleResolver.cs` (the cross-module extension check) | new. `IsExtensible` reads `@Extensible`, and `{:termination false}` only under `--modular-termination` (§5 stage 4). |
| Mixing rules (§2.3) | `ModularTermination.CheckExtensibleAncestors`, `ModuleResolver.cs`, `ResolutionErrors.cs` | new, next to the existing sealed-ancestor checks |
| Legacy dispatch as TOP (rule 9) | `ModularTermination.cs`, the dispatch classification | new. Today, with the option, every open trait is checked, so there is no legacy dispatch to classify. |
| Running the checks by use (§2.4) | `ModuleResolver.cs`, `RewriterCollection.cs`, translator gates now on `CommonOptionBag.ModularTermination` | new: one predicate, "the program uses the checked design", replaces the option test |
| Openness, effects, reach analysis, M4a | `Resolver/ModularTermination.cs`, `Resolver/ModuleResolver.cs`, `Rewriters/RewriterCollection.cs` | done (M1 to M4a, M4b milestone 2) |
| Bound comparisons | `Verifier/BoogieGenerator.Decreases.cs`, `.Methods.cs`, `Statements/BoogieGenerator.TrCall.cs`, `.ExpressionWellformed.cs`, `.Functions.Wellformedness.cs` | done. The repaired comparison is from issue 264. |
| Orders in bounds | `Resolver/DecreasesOrders.cs` | done (issue 245, M4) |
| Bounded callables | `AST/SystemModuleManager.cs`, `AST/Types/ArrowType.cs`, translator | M1 done (issue 266). Storage and return are still open. |
| Library records | `ModularTerminationRecord.cs`, `ModularTerminationTrust.cs`, `DooFile.cs`, `DafnyFile.cs`, `Backends/Library/LibraryBackend.cs` | done (M3-4, M5a, M5b). New: write a record whenever the library uses the checked design (§2.4), not only under the option. Legacy libraries' `.doo` bytes are unchanged. |
| Leaf default (§2.2) | `ModularTermination.cs` (`HasGlobalBound` and the dispatch report), `BoogieGenerator.Decreases.cs` (BOTTOM next to TOP) | new |
| Warning on legacy forms | `ModuleResolver.WarnUncheckedTermination`, `ResolutionErrors.cs` | done, default-on (https://github.com/erniecohen/dafny/pull/386) |

## 5. Staged rollout

Each stage is opt-in by use, so none needs the `AGENTS.md` exception for defaults.

**Stage 1: `@Extensible`, one PR.** The attribute, the mixing rules of §2.3, rule
9, and §2.4's predicate in place of the option test. `--modular-termination` keeps
working: it now also reads `{:termination false}` as `@Extensible` (stage 4). The PR
shows option-off byte identity on the suite and the standard library for programs
that do not use the design, and adds a test per row of §2.3's table, each negative
case with a positive companion.

**Stage 2: close what is left, one PR each:**

- (a) The one soundness gap the Reference Manual still lists (§7.1.3.2, "does not
  yet establish termination in every program"): a library verified alone does not
  see a later extension. With `@Extensible` this is the question whether a later
  `@AssumeCrossModuleTermination` extension can reach an `@Extensible` slot: §2.3
  makes it an error. Confirm with a test, in both resolvers and for `--library`
  source inputs as well as `.doo` files. Then remove the item from the manual.
- (b) The coverage arguments that M4b milestone 6 requires for higher-order code
  and implicit evaluations (constraints, witnesses, defaults, constants).
- (c) Stored and returned callables: issue 266, the milestones after M1. Without
  them `Std/Actions` cannot be certified.
- (d) The leaf default (§2.2), with an `assert false` companion for every
  positive case.

The remaining items in the manual's list of terminating programs that are rejected
are about completeness, not soundness: helper-bound inference, ranks computed
through dispatch, bounds on types, constants and iterators, and serialized input.
None of them blocks the migration.

**Stage 3: migrate our own code to `@Extensible`.** This replaces the default flip
of revision 1.

- *Standard library.* `Std/Actions` has 20 classes and traits with
  `@AssumeCrossModuleTermination`, and `Std/dfyconfig.toml` turns the warning off
  for them. Migrate per M4b §9.2 (7 openness declarations, 21 abstract slots, 26
  concrete classes, 5 stored function-valued fields), plus the UnicodeStrings, JSON
  and Strings premises that M5b leaves open. Then remove the dfyconfig opt-out. The
  fork's standard library ships with a termination record.
- *Suite.* At this base, 90 test sources use a legacy form. 15 are upstream tests
  (`auditor/TestAuditor`, five under `comp/`, `git-issue-2500`, `-4046`,
  `-4936a` to `-4936d`, `-864`, `traits/TraitMultiModule`, `traits/NonReferenceTraits`).
  Most of those test the legacy behaviour itself and stay as they are. The other 75
  are the fork's tests of issues 103, 211, 245, 264, 317, 326, 404 and the
  `traits/cardinality-*` family, with `ModularTerminationContractTests.cs`. Those that test the checked design move from
  `{:termination false}` plus `--modular-termination` to `@Extensible` without the
  option. Those that test the legacy premise or its warning (`github-issue-211-warning*`,
  the `*-false` companions that show what the premise lets through) keep the legacy
  form. A migrated test also drops the `--warn-unchecked-termination=false` that
  https://github.com/erniecohen/dafny/pull/386 added to its RUN lines.
- *Gates.* `.github/review/modular-termination-census.py`,
  `modular-termination-std.py`, `decreases-order.py` and
  `source-contracts-substitution.py` switch from the option to `@Extensible` inputs.

**Stage 4: retire `--modular-termination`.** It becomes a hidden alias with one
meaning: read every `{:termination false}` trait as `@Extensible`. That is its
meaning today, so downstream users of the option keep their checks, and it is a
migration aid: run a legacy program with it to see which traits already pass as
`@Extensible` before rewriting them. It no longer gates anything else, since §2.4
runs the checks by use. Remove it once stage 3 has moved every in-repository use,
and after one release that announces the removal. `--modular-termination-trust`
stays, for §6's question. `--legacy-cross-module-termination` from revision 1 is
not needed and is not added.

**Stage 5: release gates** (M4b milestone 6). Run the census: suite and standard
library, before and after the migration. Run both resolvers and the artifact
replacement tests. Check option-off byte identity and resource counts for programs
that do not use the design. For every migrated program that `@Extensible` rejects,
say whether it terminates, and repair the ones that do. Ask the owner to declare
the design believed sound, and drop "experimental" from the manual.

**Stage 6: upstream.** Propose the design on dafny-lang/dafny#1588, in a reply the
owner drafts and posts, with the fork's measurements. `@Extensible` as a separate
declaration suits upstream too: it adds a checked form without breaking any
existing `{:termination false}` program.

## 6. Libraries without a termination record

**Which libraries are affected.** A `.doo` file carries its source, so the
resolver can tell whether a library extends or dispatches through an `@Extensible`
trait. A library that does neither is harmless without a record: its code is TOP
and `General`, so bounded code cannot call it and `Closed` code cannot reach it.
That is sound and costs only completeness. Every existing library, including every
`.doo` from upstream Dafny, is in this group, since `@Extensible` is new.

So the question is only about a library that extends or dispatches through an
`@Extensible` trait but has no record, such as a `.doo` from an earlier fork build,
or a `--library` source input. Its implementations' bound obligations were never
verified.

**My view: agree with an error unless an explicit trust option names the library.**

- A warning would be a silent premise under `--allow-warnings`, which many builds
  set. That is the weakness PR 386 had to work around for the legacy forms, and
  `@Extensible` should not have it.
- The error costs little. It can only arise for a library built by a fork version
  that knows `@Extensible`, and the fix is to rebuild it with a current one.
- `--modular-termination-trust` already names trusted libraries (M5b). Admitting a
  library through it is a premise the user wrote down. `dafny audit` reports it,
  and the default warning fires on it like the legacy forms.

## 7. Open questions

1. Whether the leaf default (§2.2) should also apply to members of sealed traits.
   It is not needed there, since sealed dispatch stays inside one program.
2. Whether `@Extensible` should accept an argument for the default order of its
   members' bounds, to save writing `by O` on each one.
3. When to remove `--modular-termination` after stage 4: after one release, as
   proposed, or once downstream users confirm they have migrated.
