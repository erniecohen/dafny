# Plan: sound termination for traits extended across modules (issue 211)

Issue: https://github.com/erniecohen/dafny/issues/211 (upstream dafny-lang/dafny#1588).
Related: https://github.com/erniecohen/dafny/issues/245 (well-founded orders) and
https://github.com/erniecohen/dafny/issues/266 (bounded callables).
Base: `review/4.11.0` at `d7100a912`.

This plan collects the work so far into one design and one rollout. It does not
replace the detailed designs it builds on:

- [`design/211-m4b-library-openness.md`](design/211-m4b-library-openness.md): declared openness, termination
  effects, `.doo` records, and the soundness argument (its §7);
- [`design/245-orders-in-bounds.md`](design/245-orders-in-bounds.md): orders in bounds;
- [`design/266-bounded-callables.md`](design/266-bounded-callables.md): stored and returned callbacks;
- Reference Manual §7.1.3.2 and §7.1.3.3 (`docs/DafnyRef/Specifications.md`).

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

The second gap: a default `dafny verify` says nothing about the assumption. Only
`dafny audit` reports it, or `dafny verify --warn-unchecked-termination`
(https://github.com/erniecohen/dafny/pull/243, opt-in).

## 2. The design: bounds in interfaces

**Chosen design.** A trait member that other modules may implement gets a
*specification-level outer bound*, a value in a well-founded order, written
`decreases {:global} E`. Every implementation is verified once, in its own module,
against that interface. This is the sequential level discipline of Jacobs,
Bosnacki and Kuiper ("Modular Termination Verification", ECOOP 2015; TOPLAS 2018),
without call permissions. It is what `--modular-termination` implements today
(milestones M1 to M5b on issue 211). This plan makes it the one design, adds one
rule (§2.2, the leaf default), and takes it to the default.

The rules, with `B` the caller's bound captured on entry, `b` the callee's bound
evaluated at the call, `<` and `<=` the strict and weak relations of the bound's
order, and TOP above every proper bound:

1. **Openness is declared by the trait's module.** A trait may be extended from
   another module only if its defining declaration says so (`{:termination false}`,
   whose meaning becomes "extensible, checked"). A client cannot open a sealed
   trait. `@AssumeCrossModuleTermination` cannot override this.
2. **Dispatched call** through an extensible trait: `b < B`. The callee needs a
   proper bound. A call from TOP code, which has no bound, holds automatically.
3. **Known call** to code that can reach open dispatch (`General(b)`): `b <= B`,
   plus Dafny's existing `(module, SCC, decreases)` check. A known call to code
   that cannot reach dispatch (`Closed`, `{:termination_closed}` across imports)
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
8. **Libraries** (M3-4, M5): a library is admitted only with a termination record,
   written by a build that verified it in this mode. The record holds the
   library's contracts, and clients depend only on those, never on its bodies.

### 2.1 Why it is sound

Take any infinite chain of nested calls in an accepted program. Rank each
activation by the pair (outer bound, existing static metric), compared
lexicographically.

- A dispatched call or a callback application strictly lowers the outer bound
  (rules 2 and 6).
- A known call keeps the outer bound or lowers it (rule 3). When it keeps it,
  Dafny's existing `(module, SCC, decreases)` check applies. In a stretch of calls
  that keep the bound, every call is a known call, so the call graph is the one
  Dafny already reasons about inside one program, and the existing check orders it.
- An override enters the implementation at or below the slot's bound (rule 4).

The strict decreases are all in one well-founded order (rule 7), so there are
finitely many. After the last one, every call keeps the bound, and the existing
discipline rules out an infinite tail. Rule 5 makes the bound a value fixed before
the calls it orders, so the inequalities are proved without assuming the
functions whose termination they establish. The M4a counterexample (e8 on issue
211) is what happens without this rule. Rule 8 makes the argument per module.
Each implementation's obligations mention only the interface, so a body that
another module adds later cannot invalidate them. M4b §7 has the full argument,
including refinement, export views and abstract modules.

**Tie.** `A` and `B` are declared in `Tr`, so their bounds `bA(this, other)` and
`bB(this, other)` are fixed in `Tr`. Module `X` must prove
`bA(other, this) < bB(this, other)` for every `this: X`. Module `Y` must prove
`bB(other, this) < bA(this, other)` for every `this: Y`. With `this = x` and
`other = y`, these say `bA(y, x) < bB(x, y)` and `bB(x, y) < bA(y, x)`. That is
impossible in a strict order. So one of the two modules fails to verify, in that
module, without knowing the other exists. The helper `L.Tie` cannot hide the cycle.
If it has no bound, it is TOP and bounded code cannot call it. If it has a bound
`bT`, `X.B` must show `bT <= bB` and `L.Tie` must show `bB < bT`. The run above
shows each of these cases.

### 2.2 One addition: an extensible member without a bound is a leaf

Today an extensible member with no bound has none at all. Any dispatched call to it
is rejected, even from `Main` (the first row of the table). That forces a bound on
every member of every extensible trait, including the common
`function value(): nat { n }`.

Rule 2.2: **a member of an extensible trait with no declared or inherited global
bound has the bound BOTTOM**, a proper bound adjoined below every other one.

- A dispatched call to it holds from any caller, since BOTTOM is below every bound
  and below TOP.
- Its implementations run under BOTTOM. They can make no dispatched call and apply
  no callback, since nothing is below BOTTOM. They can call `Closed` code, and
  known code whose bound is BOTTOM.
- BOTTOM is the least bound in the order. Under rule 4, an override cannot raise it.

This is sound: BOTTOM is just a particular bound, and every rule above still
applies. It only accepts programs that are rejected today: dispatch from code with
no bound, to implementations that are leaves. It changes no Boogie output when the
option is off. Its cost: the order of bounds gets a least element next to TOP. That
is one more case in `ConsistentDecreasesCheck`, which already handles TOP.

### 2.3 Why not the other proposals

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

This is under the final default. Today all of it applies only with
`--modular-termination`.

1. **A trait that other modules extend** keeps `{:termination false}`, which now
   means "extensible, checked". A trait without it can be extended only in its own
   module. A client that extends a sealed trait gets an error, and the trait's
   author must declare it extensible.
2. **Members of extensible traits whose implementations are leaves** need nothing
   (§2.2).
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
7. **`@AssumeCrossModuleTermination`** has no remaining role. It is redundant when
   the trait is extensible, and an error when it is sealed.
8. **Libraries** must be rebuilt as `.doo` files in this mode (rule 8). §5 covers
   libraries without a record.

At this base, the integration tests have 90 files containing `{:termination false}`
and 22 files with `@AssumeCrossModuleTermination` (counts include the issue 211
tests). The standard library has 20 sites of `@AssumeCrossModuleTermination`, all
in `Std/Actions`. The reviewed inventory for those (M4b §9.2) is 7 openness
declarations, 21 abstract slots, 26 concrete classes and 5 stored function-valued
fields. Stage 3 measures how many programs actually need changes.

## 4. Implementation sketch

Most of the design is already in the code, behind `--modular-termination`.

| Piece | Files | State |
|---|---|---|
| Option and hidden opt-outs | `Options/CommonOptionBag.cs`, `Options/DafnyCommands.cs` | `--modular-termination` (default off), `--modular-termination-trust`, `--warn-unchecked-termination` |
| Openness, effects, reach analysis, M4a | `Resolver/ModularTermination.cs`, `Resolver/ModuleResolver.cs`, `Rewriters/RewriterCollection.cs` | done (M1 to M4a, M4b milestone 2) |
| Bound comparisons | `Verifier/BoogieGenerator.Decreases.cs`, `.Methods.cs`, `Statements/BoogieGenerator.TrCall.cs`, `.ExpressionWellformed.cs`, `.Functions.Wellformedness.cs` | done. The repaired comparison is from issue 264. |
| Orders in bounds | `Resolver/DecreasesOrders.cs` | done (issue 245, M4) |
| Bounded callables | `AST/SystemModuleManager.cs`, `AST/Types/ArrowType.cs`, translator | M1 done (issue 266). Storage and return are still open. |
| Library records | `ModularTerminationRecord.cs`, `ModularTerminationTrust.cs`, `DooFile.cs`, `DafnyFile.cs`, `Backends/Library/LibraryBackend.cs` | done (M3-4, M5a, M5b) |
| Leaf default (§2.2) | `ModularTermination.cs` (`HasGlobalBound` and the dispatch report), `BoogieGenerator.Decreases.cs` (BOTTOM next to TOP) | new |
| Warning | `ModuleResolver.WarnUncheckedTermination`, `ResolutionErrors.cs` | done, opt-in |
| Default flip | `CommonOptionBag.cs`: `--legacy-cross-module-termination` (hidden), and `ModularTermination` computed as `!legacy` | new, stage 4 |

## 5. Staged rollout

Under `AGENTS.md`, a feature that replaces an assumption with a check stays opt-in
until it is believed sound. Once it is, turning it on by default is the soundness
fix, with a hidden `--legacy-*` opt-out. The stages:

**Stage 0: report the assumption (now).** See §6.

**Stage 1: close what is left, opt-in, one PR each:**

- (a) The one soundness gap the Reference Manual still lists (§7.1.3.2, "does not
  yet establish termination in every program"): a library verified alone does not
  see a later `@AssumeCrossModuleTermination` extension. M4b milestone 2 made that
  extension an error for sealed traits, and M3-4 admits only recorded libraries.
  Confirm with a test that the gap is closed, in both resolvers and for `--library`
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
None of them blocks stage 4.

**Stage 2: migrate the standard library** under the option: `Std/Actions` per M4b
§9.2, plus the UnicodeStrings, JSON and Strings premises that M5b leaves open. The
fork's standard library then ships with a termination record.

**Stage 3: release gates** (M4b milestone 6). Run the four-way census: suite and
standard library, option on and off. Run both resolvers and the artifact
replacement tests. Check option-off byte identity and resource counts. For every
program that the option rejects, say whether it terminates. Repair the ones that
do, in the same PR where possible. Ask the owner to declare the mode believed
sound.

**Stage 4: flip the default.** Turn `--modular-termination` on by default. The
hidden `--legacy-cross-module-termination` restores 4.11's behaviour byte for byte,
for comparison and migration only. `--modular-termination` remains as an accepted
no-op. The PR lists every suite and standard-library verdict that differs between
the default and the opt-out, with a reason for each, as `AGENTS.md` requires. Only
programs that verified because of the unchecked assumption may change verdict.
Libraries without a record, such as a `.doo` from upstream Dafny, are admitted only
as a premise the user chose. They are named in a warning, so they are never
admitted silently.

**Stage 5: upstream.** Propose the design on dafny-lang/dafny#1588, in a reply the
owner drafts and posts, with the fork's measurements.

## 6. What `dafny verify` should report today

Today, a default `dafny verify` of the Tie program above prints
`8 verified, 0 errors` and nothing else. The program proves `false`. An `assume`
without `{:axiom}` in the same program would be reported. So the minimum fix is to
report `{:termination false}` and `@AssumeCrossModuleTermination` the same way.

https://github.com/erniecohen/dafny/pull/243 does exactly that, behind
`--warn-unchecked-termination`. On the program above, it prints:

```
tie.dfy(3,29): Warning: trait 'T' has {:termination false} and no {:axiom} annotation: calls dispatched through it are not checked for termination
```

It reports only declarations that the run verifies. Libraries and included files
are left out, since their users cannot annotate them. `{:axiom}` on the declaration,
or `--allow-axioms`, acknowledges the assumption.

**Recommendation: turn this warning on by default, with a hidden opt-out
`--legacy-silent-unchecked-termination`.** That needs an owner decision, because it
departs from two existing rules:

- `AGENTS.md`, "What may change the defaults": a new warning is not a soundness,
  crash or message fix. Under Dafny's default `allow-warnings = false`, it turns a
  passing `verify` into a failing one (exit 2). PR 243 measured 60 integration-test
  files that would change.
- The owner's comment on issue 211 placing the warning behind an opt-in option.

The case for an exception is that the warning reports an unchecked assumption in
code the user is verifying. That is the same reason upstream warns on `assume`
without `{:axiom}`, by default. It changes no verdict and no Boogie program, and
`{:axiom}` silences it in one place per declaration. If the owner agrees, the PR is
small. It changes the option's default and adds the hidden opt-out. It adds
`{:axiom}` to the suite files that do not test the warning, or `--allow-warnings`
to their RUN lines. It adds a release note. It does not cover the standard library,
whose users are not warned.

Under `--modular-termination` the warning stays, with the text PR 243 gives it,
until stage 4. After stage 4, `{:termination false}` is checked and no longer
warned about. The warning then fires only under `--legacy-cross-module-termination`,
and for libraries admitted without a record.

## 7. Open questions

1. The owner's decision on the default-on warning (§6).
2. Whether to spell openness `@Extensible` as an alias of `{:termination false}`,
   whose name reads as "assume" when it now means "checked".
3. Whether the leaf default (§2.2) should also apply to members of sealed traits.
   It is not needed there, since sealed dispatch stays inside one program.
4. The policy for libraries without a record after stage 4: a warning, as proposed,
   or an error unless an explicit trust option is given.
