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

**Revisions.**

- Revision 1 proposed reading `{:termination false}` as "extensible, checked" and
  flipping the default.
- Revision 2: the owner chose not to break legacy uses of `{:termination false}`.
  It proposed a new `@Extensible` declaration instead.
- Revision 3, this one: the owner wrote, "The interface bound (if any) is explicitly
  declared ... if it is not declared, it is implicitly 0 ... So I'm suggesting that
  the visibility of all interface bounds should make @extensible redundant." So
  there is no openness marker. Every trait may be extended from any module, and
  each member's declared bound, or the lack of one, says what its implementations
  may do.

The owner's proposal, as given, has two holes, both shown by programs that prove
`false` (§2.2). The repair is small, and the rest of the proposal survives it:

- a member with a declared bound binds every implementation, including those in
  the trait's own module;
- a member without one is not a leaf for its callers, only for implementations in
  other modules.

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
given. What is missing is a way for other modules to extend a trait with Dafny
checking the result.

## 2. The design: bounds in interfaces, no openness marker

A member's *interface bound* is a value in a well-founded order, written
`decreases {:global} E`. It is part of the member's signature, so it is visible
wherever the member is. This is the sequential level discipline of Jacobs, Bosnacki
and Kuiper ("Modular Termination Verification", ECOOP 2015; TOPLAS 2018), without
call permissions. Most of it is in the code today, behind `--modular-termination`
(milestones M1 to M5b on issue 211).

Terms used below:

- The **home** of a member is the module that first declares it. An implementation
  in another module is **foreign**, even when it sits in a subtrait declared in that
  other module.
- `B` is the caller's bound, captured on entry. Code with no bound has TOP, above
  every proper bound.
- `b` is the callee's bound, evaluated at the call. `<` and `<=` are the strict and
  weak relations of the bound's order.

The rules:

1. **Every trait may be extended from any module.** There is no openness marker.
2. **Declared members.** A member with a declared bound binds every implementation,
   home or foreign, to that bound. A call dispatched to it needs `b < B`.
3. **Undeclared members, foreign side.** A foreign implementation of a member with
   no declared bound must be a **leaf**. A leaf makes no dispatched call through
   any trait and applies no function value. It calls only code that, transitively,
   does neither. Evaluations that Dafny makes implicitly count too: constant
   initializers, default parameter values, subset-type constraints and witnesses,
   and calls in specifications. A leaf is stronger than today's `Closed`, which
   excludes only open dispatch.
4. **Undeclared members, home side.** A home implementation of a member with no
   declared bound keeps today's check: override edges in the home module's call
   graph, under `(module, SCC, decreases)`. In exchange, a call dispatched to such
   a member counts as TOP for a bounded caller, which cannot make it, directly or
   through a known call. A caller with no bound may make it, as today.
5. **Explicit leaf.** A member may be declared a leaf (spelling open, §7). Then
   every implementation, home or foreign, must be a leaf, and the member's bound
   is BOTTOM, a proper bound below every other one. Any caller may dispatch to it.
   This is revision 2's leaf default (`function value(): nat`), made explicit.
6. **Known call** from code with bound `B` to code that can reach dispatch or
   application at bound `b`: `b <= B`, plus the existing `(module, SCC, decreases)`
   check. A known call to `Closed` code needs only the existing check. `Closed`
   code reaches no application, and no dispatch other than to explicit leaves.
   Today's `Closed` excludes only open dispatch, but under rule 4 a dispatch to an
   undeclared member counts as TOP, so `Closed` must exclude it as well. A leaf is
   `Closed` code that does not dispatch to explicit leaves either.
7. **Overrides** inherit the bound. An explicit one must be `<=` every inherited
   bound. A foreign override of an undeclared member cannot declare a bound, since
   it would raise the member from leaf to something above it.
8. **Bounds are closed** (M4a): a bound, and the order it uses, may not reach
   dispatch or application. Its value cannot depend on the code whose termination
   it orders.
9. **Function values** (M3, issue 266): applying a function value whose code is not
   known is TOP. A callback parameter with `decreases {:callback f} E`, or a value
   of type `bounded<O, F>`, carries checked evidence that its code runs under `E`.
10. **Orders** (issue 245, issue 264): any order with a well-foundedness proof
    (`decreases {:global} e by O`), whose weak and strict relations satisfy
    `a <= b < c  ==>  a < c`.
11. **Libraries** (M3-4, M5): a library with a foreign implementation, or a
    declared bound, is admitted only with a termination record (§6).
12. **Legacy forms**: §2.4.

### 2.1 Why it is sound

Compare an accepted program with the same program minus its foreign
implementations of non-legacy traits. Up to rule 1, the smaller program is one that
Dafny accepts today, so today's checks apply to it. The only new edges are dispatches
into foreign code, and rules 2 to 4 make them one-way:

```
TOP code  ──dispatch to a declared member──▶  bounded code  ──▶  leaf code
   ▲                                               │                 │
   └───────────────── never ───────────────────────┴─────────────────┘
```

- **Leaf code** (rule 3) calls only code that makes no dispatch and no application.
  From there on, every call is a static call, and static calls follow the import
  order between modules and today's check within one module. So every chain that
  enters a leaf ends.
- **Bounded code** (rules 2, 6, 8, 9) reaches only bounded, `Closed` or leaf code.
  Each dispatch and each callback strictly lowers the bound, and a known call keeps
  it or lowers it. Rule 4 is what keeps it there: an undeclared member's home
  implementation is TOP code, so a bounded caller cannot dispatch to it. With
  rule 10, there are finitely many strict steps. After the last one the chain makes
  only known calls at one bound, which today's check orders. So every chain that
  enters bounded code ends.
- **TOP code** that never enters bounded or leaf code makes only calls that exist in
  the smaller program. Its dispatches land in home implementations, since a foreign
  one is bounded or a leaf. Today's check covers that program.

So no infinite chain of calls exists, unless it uses a legacy premise (§2.4).
Rule 8 keeps the bound inequalities free of the functions they order (the M4a
counterexample, e8 on issue 211). Rule 11 makes the argument per module: each
implementation's obligations mention only interfaces. M4b §7 has the full argument
for refinement, export views and abstract modules. Replace "extensible trait" there
by "member with a declared bound", and add rule 3's leaves as a third kind of
effect below `Closed`.

**Tie.**

- *Without bounds:* `X.B` and `Y.A` are foreign implementations of undeclared
  members that dispatch, so both are rejected as non-leaves (rule 3).
- *With bounds* `bA(this, other)` and `bB(this, other)` declared in `Tr`, it is
  rejected as before. `X` must prove `bA(y, x) < bB(x, y)` and `Y` must prove
  `bB(x, y) < bA(y, x)`, which no strict order allows.
- *The helper `L.Tie`:* if it has no bound, it is TOP and bounded code cannot call
  it. If it has a bound `bT`, `X.B` must prove `bT <= bB` and `L.Tie` must prove
  `bB < bT`.

### 2.2 Two attacks on the proposal as given, and the repair

The proposal as given said: undeclared members are BOTTOM for everyone, and home
implementations are exempt from the interface bound. Both attacks below were run with
default options and with `--modular-termination`, on a build of
https://github.com/erniecohen/dafny/pull/386's head with Z3 5.1.0. Each uses
`{:termination false}`, the only way today to give `Tr.T` a foreign implementation.

**Attack 1: a foreign bounded implementation dispatches to an undeclared member
whose home implementation is exempt.**

```dafny
module Tr {
  trait {:termination false} T {
    function A(other: T): int                            // undeclared: "BOTTOM"
    function B(other: T): int decreases {:global} 1
  }
  class X extends T { constructor () { }
    function A(other: T): int { other.B(this) }          // home: exempt, 1 < TOP
    function B(other: T): int decreases {:global} 1 { 0 } }
}
module Y { import Tr
  class Y extends Tr.T { constructor () { }
    function A(other: Tr.T): int { 0 }                    // foreign leaf
    function B(other: Tr.T): int decreases {:global} 1 { other.A(this) + 1 } } } // BOTTOM < 1
module M { import Tr import Y
  method Main() {
    var x: Tr.T := new Tr.X();  var y: Tr.T := new Y.Y();
    assert x.A(y) == y.B(x) == x.A(y) + 1;
    assert false;
  } }
```

Every rule of the proposal as given holds. Default options: `7 verified, 0 errors`,
with the default warning on `T`. `--modular-termination` rejects it at `Y.B`'s call
`other.A(this)`, because the callee has no bound. That rejection is rule 4: entering
the home implementation `X.A` raises the chain from BOTTOM back to TOP.

**Attack 2: the home implementation of a declared member is exempt.** `A` has bound
2 and `B` bound 1. The home `X.B` dispatches to `A`, and the foreign `Y.A` (bound 2)
dispatches to `B` (1 < 2), adding 1. Then `x.B(y) == x.B(y) + 1`. Default options:
`7 verified, 0 errors`, with the warning. `--modular-termination` rejects it at `X.B`
("the dispatched callee's bound must be below the caller's"). That rejection is
rule 2: a declared bound binds home implementations too.

Both repairs leave the compatibility claim intact (§2.5). No existing program
declares a bound. And no existing program has a bounded caller, so none
dispatches to an undeclared member from one.

### 2.3 Further attacks

Programs this design accepts each need an `assert false` control in their test.
"Today" means review/4.11.0 with default options.

| Attack | Verdict under this design | Why |
|---|---|---|
| Tie with no bounds | rejected at `X.B` and `Y.A` | Foreign non-leaves (rule 3). Today it is a resolution error without `{:termination false}`. |
| A foreign leaf `Y.A` calls back into `Tr` directly, to a function that dispatches to `B` | rejected | It reaches dispatch (rule 3). |
| The same through a helper in `Tr`, or in a third module `L` | rejected | Rule 3 is transitive and covers calls into any module. |
| A foreign leaf calls a static function of `Tr` that makes no dispatch and no application | accepted; control: `assert false` fails | A static call follows imports. `Tr` cannot call `Y` except by dispatch. |
| A foreign subtrait `S.U extends Tr.T` adds a member `C` with a declared bound | accepted; control | `C`'s home is `S`. Rule 2 binds every implementation of `C`. |
| `S.U` overrides undeclared `Tr.T.A` with a body | must be a leaf | Foreign to `A`'s home (rule 3). Declaring a bound on it is an error (rule 7). |
| `S.U` adds an undeclared member `C`, and a class in `S` implements it with dispatch | accepted, as home code; control | Rule 4 in `S`. A class in a fourth module implementing `C` must be a leaf. |
| A bounded implementation dispatches to `value()`, undeclared, whose home implementation is a leaf | rejected | Rule 4 does not look at bodies. Declaring `value()` an explicit leaf (rule 5) makes it accepted. |
| A foreign leaf stores a function value in a field, and home TOP code later applies it | accepted, as today | Storing is no call. Applying from TOP code is a plain-Dafny edge that exists today without any foreign implementation: a client can pass `Tr` a lambda. Bounded code cannot apply it (rule 9). |
| A foreign leaf applies a stored function value | rejected | Rule 3. |
| A foreign leaf reads an instance constant of `T` whose initializer dispatches; or calls a function whose default parameter value dispatches; or tests membership in a subset type whose constraint dispatches; or has a precondition that dispatches | rejected, once the implicit evaluations are covered | This is stage 2(b). Until it lands, these are the known gaps listed in the manual. M4b milestone 6 already requires the coverage argument for `Closed`. |
| Module `R` refines `Tr` and adds classes extending `R.T` | `R`'s classes are home to `R.T` | `R` is verified with all of them. Refinement may add a declared bound or an explicit leaf, never drop one (the M4b refinement check). A client of an abstract import `A : Tr` relies only on `Tr`'s declarations. |
| A foreign implementation through an export that reveals `T` but provides only some members | checked by member | Bounds are part of signatures. The M4b view tests (`views`, `views-bad`, `views-false`) carry over. |

Attacks 1 and 2 are the ones that succeeded against the proposal as given. None of
the others succeeds against the repaired rules, but only the first two were run. The
rest are reasoned from the rules above, and each becomes a test in stage 1 or 2.

### 2.4 Legacy forms, and programs that mix them

`{:termination false}` and `@AssumeCrossModuleTermination` keep their 4.11 meaning:
an unchecked premise. They are warned by default since
https://github.com/erniecohen/dafny/pull/386 (hidden opt-out
`--legacy-no-unchecked-termination-warning`). `dafny audit` reports them, and
`{:axiom}` or `--allow-axioms` acknowledges them. Under rule 1 neither is needed any
more to allow an extension. What each one still does is exempt foreign
implementations of undeclared members from rule 3.

| Situation | Verdict | Reason |
|---|---|---|
| A `{:termination false}` trait, members without bounds | as in 4.11 | Foreign implementations of its undeclared members are unchecked: the premise. A call dispatched to them is TOP for bounded callers (rule 4). |
| A `{:termination false}` trait whose members also declare bounds | allowed; the declared members are checked | A bound is part of the member's signature, and rule 2 binds every implementation, whatever the trait says. The attribute covers only undeclared members. When every member has a bound or is an explicit leaf, the attribute assumes nothing and is still warned. That is the cue to delete it. |
| A type with `@AssumeCrossModuleTermination` extending a trait without `{:termination false}` | allowed; the premise covers only its implementations of undeclared members | Those may be non-leaves, unchecked. Its implementations of declared members and explicit leaves are checked: one client must not weaken a bound that the trait's author declared, which other modules rely on. No existing program declares one, so nothing breaks. |
| A bounded caller reaches a call dispatched through a legacy trait | error | Rule 4 already treats it as TOP. This was rule 9 of revision 2. |
| A legacy premise lets a TOP chain cycle through foreign code | not excluded | That is the premise. §2.1's argument holds for every chain that never uses one. Bounded code cannot reach legacy dispatch, so a declared bound stays sound whatever the legacy premises say. |

### 2.5 Compatibility, and when the checks run

Claim (f) holds. Run on PR 386's head, extending `Tr.T` from module `Y` without a
legacy form is rejected at resolution: "class 'Y' is in a different module than trait
'Tr.T'. A class may only extend a trait in the same module, unless ...". So:

- rule 1 only accepts programs that are rejected today;
- rule 3 applies only to foreign implementations that are an error today, since
  legacy ones are exempt (§2.4);
- rules 2, 5 and 6 to 9 only affect code that declares a bound, an explicit leaf, a
  callback bound or a `bounded<O, F>` type. Upstream Dafny has none of these;
- rule 4 only restricts bounded callers.

A program with none of these goes through the 4.11 code paths, with identical
Boogie output. The checks run when a program has a declared bound, an explicit
leaf, a callback bound, a `{:termination_closed}` promise, a `bounded<O, F>` type, or
a foreign implementation of a non-legacy trait, in its own code or in a library's
record.

One existing behaviour changes meaning: `decreases {:global} E` is read as an
interface bound without an option. Today, without `--modular-termination`, it is an
ordinary decreases clause. At this base, 60 test sources use `{:global}`, all of
them the fork's own tests of issues 211, 245 and 264. Stage 3 migrates them.

The checks only add obligations where the legacy forms assumed them, so this can
ship before stage 5 is complete, marked experimental in the Reference Manual, with the
gaps of stage 2 listed there.

### 2.6 Why not the other proposals

| Proposal (dafny-lang/dafny#1588) | Soundness | Modularity | Burden |
|---|---|---|---|
| Cycle check on module signatures (keyboardDrummer) | Rejects Tie | No. The error appears in `M`, the module that imports both `X` and `Y`, and it depends on which implementations are in scope there. A library cannot be verified once. | No annotations, but rejects every terminating cross-module cycle, such as the counter version of Tie. |
| `calls` clauses (keyboardDrummer) | Defeated by Tie | — | — |
| Required `decreases` on bodiless trait members (Mayer) | Sound only if those decreases are compared across modules. Then they are the interface bounds of this design under another name, and they also need rules 4, 6 and 8. | Yes, once made precise | Higher: a bound on every member, including leaves |
| Module and SCC components in decreases, with exported ordering edges (Leino) | Sound | Partial: modules publish their internal SCC order | Heavy: names of SCCs in specifications |
| This design | Sound (§2.1) | Yes: clients depend on interfaces only (M4b §8) | None for foreign leaves. A bound only on members whose foreign implementations dispatch or call back. |

The choice rests on two principles. Termination of dispatch has to be a property
of the interface, since the implementation is unknown. And a client may depend
only on what a library publishes. Of the proposals above, only this one has both.

## 3. What user code must change

Nothing, to keep verifying as in 4.11. The legacy forms keep their meaning and their
default warning.

To extend a trait from another module, checked:

1. **Leave out `{:termination false}` and `@AssumeCrossModuleTermination`.** Any
   trait may be extended.
2. **Foreign implementations of members without a bound must be leaves** (rule 3).
   Most implementations of getters, predicates and pure computations already are.
3. **A member whose foreign implementations must dispatch or apply callbacks** needs
   `decreases {:global} E`, and every implementation, home ones included, must
   prove the strict decrease at each dispatched call (rule 2). The order may be
   any order of issue 245.
4. **A member that bounded code dispatches to** needs a declared bound, or an
   explicit leaf declaration (rules 4 and 5).
5. **Helpers that forward a dispatched call from bounded code** need a bound of
   their own, at most the caller's, or `{:termination_closed}` if they cannot reach
   dispatch. Dafny does not infer helper bounds.
6. **Callbacks** need `decreases {:callback f} E` on the parameter, or the type
   `bounded<O, F>` if the value is stored or returned (issue 266).
7. **A bound may not be computed through dispatch** (rule 8).
8. **Libraries** with foreign implementations or declared bounds are rebuilt as
   `.doo` files with a termination record (rule 11).

## 4. Implementation sketch

| Piece | Files | State |
|---|---|---|
| Rule 1: drop the cross-module extension error | `ModuleResolver.cs` (around the "is in a different module than trait" error), `ResolutionErrors.cs` | new. The legacy forms stay recognized, for §2.4 and the warning. |
| Openness per member, not per trait | `Resolver/ModularTermination.cs` (`IsExtensible` is replaced by "has a declared bound", "explicit leaf" and "home module of the member") | new |
| Leaf effect (rule 3) and foreign-implementation check | `ModularTermination.cs`: a third effect below `Closed`, computed over the same hooks (constants, defaults, constraints, witnesses, iterators, specifications) | new |
| Rule 4: undeclared dispatch is TOP for bounded callers | `ModularTermination.cs`, `BoogieGenerator.Decreases.cs` | done in effect. Today's option already rejects bounded dispatch to an unbounded member (attack 1). New: it no longer rejects dispatch from TOP callers (the first row of the table in §1). |
| Rule 2: home implementations bound | `BoogieGenerator.Decreases.cs`, `.Methods.cs`, `TrCall.cs` | done (attack 2 is rejected today) |
| Explicit leaf (rule 5) | `Attributes.cs`, `ModularTermination.cs`, BOTTOM next to TOP in `BoogieGenerator.Decreases.cs` | new |
| Running the checks by use (§2.5) | the gates on `CommonOptionBag.ModularTermination` in `ModuleResolver.cs`, `RewriterCollection.cs` and the translator | new: one predicate replaces the option test |
| Bound comparisons, orders, M4a | `BoogieGenerator.*`, `Resolver/DecreasesOrders.cs` | done (issues 211, 245, 264) |
| Bounded callables | `AST/SystemModuleManager.cs`, `AST/Types/ArrowType.cs`, translator | M1 done (issue 266). Storage and return are still open. |
| Library records | `ModularTerminationRecord.cs`, `ModularTerminationTrust.cs`, `DooFile.cs`, `DafnyFile.cs`, `Backends/Library/LibraryBackend.cs` | done (M3-4, M5a, M5b). New: record each member's bound or leaf declaration and each function's effect, written whenever the library uses the design. Legacy libraries' `.doo` bytes are unchanged. |
| Warning on legacy forms | `ModuleResolver.WarnUncheckedTermination` | done, default-on (PR 386) |

## 5. Staged rollout

Each stage is opt-in by use, so none needs the `AGENTS.md` exception for defaults.

**Stage 1: rules 1 to 5, one PR.**

- Implement rules 1 to 5, and §2.5's predicate in place of the option test.
- Show option-off byte identity on the suite and the standard library.
- Add a test for each attack in §2.2 and §2.3, and for each row of §2.4. Each test
  is the attack program, with an `assert false` control wherever the design accepts
  it.

**Stage 2: close what is left, one PR each:**

- (a) The soundness gap the Reference Manual still lists (§7.1.3.2): a library
  verified alone does not see a later extension. Under rule 3, a later foreign
  implementation is a leaf, bounded, or a legacy premise. Confirm this with tests in
  both resolvers, for `--library` source inputs and for `.doo` files. Then remove
  the item from the manual.
- (b) The coverage of implicit evaluations (constants, defaults, constraints,
  witnesses, specifications) by the leaf and `Closed` effects, which M4b
  milestone 6 requires.
- (c) Stored and returned callables: issue 266, the milestones after M1. Without
  them `Std/Actions` cannot be certified.

The remaining items in the manual's list of terminating programs that are rejected
are about completeness, not soundness: helper-bound inference, ranks computed
through dispatch, bounds on types, constants and iterators, and serialized input.

**Stage 3: migrate our own code.** Migration removes `{:termination false}` and
`@AssumeCrossModuleTermination` wherever the program now passes without them, and
adds bounds where it does not.

- *Standard library.* `Std/Actions` has 20 classes and traits with
  `@AssumeCrossModuleTermination`. Migrate them per M4b §9.2: 7 openness
  declarations, 21 abstract slots, 26 concrete classes, 5 stored function-valued
  fields. Under this design the 7 openness declarations disappear. Then remove the
  warning opt-out from `Std/dfyconfig.toml`, and close the UnicodeStrings, JSON and
  Strings premises that M5b leaves open. The fork's standard library ships with a
  termination record.
- *Suite.* At this base, 90 test sources use a legacy form.
  - *Upstream tests, 15:* `auditor/TestAuditor`, five under `comp/`,
    `git-issue-2500`, `-4046`, `-4936a` to `-4936d`, `-864`,
    `traits/TraitMultiModule` and `traits/NonReferenceTraits`. Most of them test
    the legacy behaviour itself and stay as they are.
  - *The fork's tests, 75:* issues 103, 211, 245, 264, 317, 326 and 404, the
    `traits/cardinality-*` family, and `ModularTerminationContractTests.cs`. Those
    that test the checked design drop the marker and `--modular-termination`.
  - *Tests of the premise or its warning,* such as `github-issue-211-warning*` and
    the `*-false` companions, keep the legacy form.

  A migrated test also drops the `--warn-unchecked-termination=false` that PR 386
  added to its RUN lines.
- *Gates.* `.github/review/modular-termination-census.py`,
  `modular-termination-std.py`, `decreases-order.py` and
  `source-contracts-substitution.py` switch from the option to unmarked inputs.

**Stage 4: retire `--modular-termination`.**

- It becomes a hidden migration aid with one meaning: check the program as if
  `{:termination false}` and `@AssumeCrossModuleTermination` were absent.
  Running a legacy program with it shows which markers can simply be deleted.
- It no longer gates anything else, since §2.5 runs the checks by use.
- Remove it once stage 3 has moved every use in this repository, after one release
  that announces the removal.
- `--modular-termination-trust` stays, for §6.

**Stage 5: release gates** (M4b milestone 6).

- Run the census on the suite and the standard library, before and after the
  migration.
- Run both resolvers and the artifact replacement tests.
- Check byte identity and resource counts for programs that do not use the design.
- For every program that loses its marker and is then rejected, say whether it
  terminates, and repair the ones that do.
- Ask the owner to declare the design believed sound, and drop "experimental" from
  the manual.

**Stage 6: upstream.** Propose the design on dafny-lang/dafny#1588, in a reply the
owner drafts and posts, with the fork's measurements. Upstream needs no new
keyword: unmarked cross-module extension becomes legal, leaves need nothing, and
`{:termination false}` keeps working.

## 6. Libraries without a termination record

A `.doo` file carries its source, so the resolver can tell whether a library
declares bounds or has foreign implementations of non-legacy traits.

- **A library with neither** is harmless without a record. Its code is TOP and not a
  leaf, so bounded code cannot call it and leaves cannot reach it. That is sound,
  and costs only completeness. Every existing library is in this group, including
  every `.doo` from upstream Dafny, since a foreign implementation of a non-legacy
  trait was an error until rule 1.
- **A library with either** but no record, such as a `.doo` from an earlier fork
  build or a `--library` source input, is one whose leaf and bound obligations were
  never verified.

**My view: for the second group, an error unless an explicit trust option names the
library.**

- A warning would become a silent premise under `--allow-warnings`, which many builds
  set. That is the weakness that PR 386 had to work around for the legacy forms.
- The error costs little. It can arise only for a library built by a fork version
  that has rule 1, and the fix is a rebuild.
- `--modular-termination-trust` already names trusted libraries (M5b). A library
  admitted that way is a premise the user wrote down. `dafny audit` reports it, and
  the default warning fires on it like the legacy forms.

## 7. Open questions

1. The spelling of the explicit leaf (rule 5). Candidates are an attribute on the
   member, such as `{:termination_leaf}`, or `decreases {:global} BOTTOM`. The
   owner's "implicitly 0" suggests the second, but 0 of `nat` is not below the
   bounds of other orders.
2. Whether rule 4 should let a bounded caller dispatch to an undeclared member whose
   home implementations are all leaves. That would make a body-derived fact part of
   the interface, which the fork's rules forbid ("private bodies never strengthen an
   imported contract"). The explicit leaf is the alternative.
3. Whether a `{:termination false}` trait whose members all have bounds or leaf
   declarations should get a "redundant" hint instead of the premise warning.
4. When to remove `--modular-termination` after stage 4: after one release, as
   proposed, or once downstream users confirm they have migrated.
