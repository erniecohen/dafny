# #211 M4b: modular termination across library boundaries

**Status:** design for review; no product implementation is included in this PR.

**Target:** `review/4.11.0`, reviewed at `d0301c6f702dabada677d99c3ebf63745f357d9c`.

**Request:** [the M4b design request][request].

**Decision:** reverify the complete retained-source dependency closure in the consumer's final resolved program whenever full verification uses `--modular-termination`. Do not trust a library's previous verification result. Do not change the `.doo` format in M4b.

## 1. Recommendation and scope

Choose alternative (a), strengthened from “recheck openness-dependent obligations” to **recheck all verification obligations of the loaded source closure**. The consumer already reparses and resolves a `.doo`'s retained source. Under the option it must also verify that source, including its specifications, ordinary termination, modular termination, overrides, constraints, witnesses and other generated obligations. Restore verification eligibility independently of compilation eligibility.

This is a deliberately simple soundness boundary:

> A library proof from an earlier composition is never used to omit a proof in the current composition. A complete successful check belongs to the current resolved program, its effective options and its actual proof tasks.

Retain the current extension-sensitive definition of an open trait. A new foreign implementation can make previously closed code open; the consumer then checks that code again. There is no need to treat every exported trait as open merely because it could be extended in some future program. Conversely, a future extension is not entitled to reuse a library's old proof without another check.

This establishes an **extension-checked composition** property: every final composition that passes the complete check satisfies the termination rules. It does not establish that every future extension will be accepted, or that independently supplied native binaries satisfy retained Dafny source. Those are different claims.

The original design's reusable “closed legacy”, “open modular” and “assumed” certificates remain useful distinctions, but M4b does **not** implement a portable certificate protocol. True open certificates, which survive conforming extensions without rechecking a library, are deferred. Exact result reuse can later optimize this design through [#241 and its proposed study][cache-study], after current global admission. This document specifies that boundary; correctness does not depend on a cache being implemented.

The design is for the shipped M1–M4a rules, not the original plan's unimplemented method bags, helper-bound inference, arbitrary stored function-value evidence or stratified rank computations. `{:termination false}` and `@AssumeCrossModuleTermination` continue to admit foreign extension as today; under the option they do not excuse the M1–M4a obligations. The separate default-warning work is not part of this change. [Current specification][spec]; [original proposal and attachment][original]; [review][original-review]; [M4a design][m4a]; [approval][m4a-approval].

## 2. Evidence: the present gap is reproducible

### 2.1 A library and a later implementation

The following is an executed reproducer, not just a proposed test. Save the first module in `lib.dfy`:

```dafny
module A {
  trait T {
    function {:fuel 3,4} f(n: nat): nat
      decreases {:global} n
  }

  function {:fuel 3,4} H(t: T, n: nat): nat
    decreases {:global} n
  {
    t.f(n) + 1
  }
}
```

Save this in `client.dfy`:

```dafny
module B {
  import A

  @AssumeCrossModuleTermination
  class C extends A.T {
    function {:fuel 3,4} f(n: nat): nat {
      A.H(this, n)
    }
  }

  method Main() {
    var c := new C;
    var n := c.f(0);
    assert false;
  }
}
```

`A.T` is closed when the library is checked alone. In the consumer it is open. The call `C.f -> A.H` satisfies `n <= n`; the newly open call `A.H -> T.f` must fail `n < n`. Currently the library's check is skipped. The contradictory recursive function equations then allow the client to prove `false`.

### 2.2 Commands and observed results

The public release [v4.11.0+fcb2042d.review.06a2267a][release] corresponds to the reviewed commit. The release archive checksum was checked against its published checksum. The probes used Z3 5.1.0, one core and a 20-second verification limit. No modified verifier was used, and no nonterminating program was executed.

With that `dafny` on the command path, set `z3_211` to the Z3 5.1.0 executable's path and run:

```sh
dafny build -t:lib lib.dfy --modular-termination --solver-path:"$z3_211" --output:lib-on.doo --cores:1 --verification-time-limit:20
dafny build -t:lib lib.dfy --solver-path:"$z3_211" --output:lib-off.doo --cores:1 --verification-time-limit:20
dafny verify lib-on.doo client.dfy --modular-termination --solver-path:"$z3_211" --cores:1 --verification-time-limit:20
dafny verify lib-off.doo client.dfy --modular-termination --solver-path:"$z3_211" --cores:1 --verification-time-limit:20
dafny verify lib.dfy client.dfy --modular-termination --solver-path:"$z3_211" --cores:1 --verification-time-limit:20
```

The explicit solver selection avoids depending on the release's default solver discovery.

| Experiment | Observed result on the base |
| --- | --- |
| Build library alone, option on | Exit 0; 1 verified, 0 errors |
| Build library alone, option off | Exit 0; 1 verified, 0 errors |
| On-produced `.doo`, consumer on | **Exit 0; 3 verified, 0 errors, including `assert false`** |
| Off-produced `.doo`, consumer on | **Exit 0; 3 verified, 0 errors, including `assert false`** |
| On-produced `.doo`, consumer off | Exit 0; 2 verified, 0 errors; legacy behavior |
| Off-produced `.doo`, consumer off | Exit 0; 2 verified, 0 errors; legacy behavior |
| Library and client supplied together as source, consumer on | **Exit 4; 3 verified, 1 error at `H`'s dispatched call** |
| On-produced `.doo`, consumer on with `--type-system-refresh` | Exit 0; 3 verified, 0 errors, including `assert false` |
| Combined source, consumer on with `--type-system-refresh` | Exit 4; 3 verified, 1 error at the same call |

The intended error is “the dispatched callee's 'decreases {:global}' bound must be below the caller's”. The refreshed-resolver experiment used the on-produced artifact; the off-produced/refreshed combination is an implementation regression requirement below, not an additional claimed experiment.

This is evidence for the bug and for the need to restore the missing obligation. It is not a verification of the proposed implementation, a full soundness proof of M1–M4a, or a census of existing programs.

### 2.3 Why the existing pipeline permits it

The following facts are from the pinned source:

| Location | Present behavior and consequence |
| --- | --- |
| [`DooFile`][doo], [`LibraryBackend.Compile`][library-backend] | The archive contains `manifest.toml` and `program`; `program` is source serialized from `AfterParsingClone`. It is not a proof certificate. |
| [`DafnyFile.HandleDooFile`][dafny-file] | Sets `ShouldNotVerify = true` for every `.doo`. Source `--library` inputs and source embedded in DLLs have analogous exclusions. |
| [`ProgramParser.ParseFiles`][program-parser], [`ShouldCompileOrVerify.ShouldVerify`][scope] | `AlreadyVerifiedRoots` and its include closure remove library URIs before the `VerifyAllModules` test. Setting `VerifyAllModules` alone cannot fix this. |
| [`ModularTerminationRewriter.PostResolve`][mt] | Skips members failing `ShouldVerify`, including M4a bound-closure checks. Running the rewriter again without changing proof scope is insufficient. |
| [`TextDocumentLoader.ResolveInternal`][loader], [`Compilation.VerifyCanVerify`][compilation], [`BoogieGenerator.InVerificationScope`][boogie] | Symbol discovery, scheduling and translation have further verification-scope gates. They must agree on the restored obligations. |
| [`ModularTermination.OpenTraits`][mt] | Uses `{:termination false}`, foreign parents actually present in this program, and their ancestors. Later extensions change the analysis. |
| [`ScopeCloner`][scope-cloner] | Export views erase method bodies, provided function bodies and hidden constant initializers. Missing visible bodies cannot establish that code is closed. |
| [`BoogieGenerator.Assert`][boogie], [`CheckCallTermination`][decreases] | Inherited assertions may become assumptions; the existing force-check condition does not cover every inherited call with inherited global bounds. Fresh proof scope alone does not remove this reuse path. |

`ModularTermination` is a CLI-scoped option and is absent from the `.doo` manifest. The parser interprets the marked decreases clauses differently depending on that option. In the probes both producers serialized identical `program` and manifest payloads. Both manifests reported `dafny_version = "4.11.0.0"`, although the executable displays the longer fork product version. The current version comparison therefore does not identify an exact fork proof build. These facts rule out interpreting the existing manifest as evidence of modular termination. [Options][options]; [parser][parser]; [manifest validation][doo].

## 3. Exact admission and verification rules

### 3.1 Input snapshots and the dependency closure

For a complete option-on verification, capture an input inventory as each source is first loaded, including files discovered by parsing includes. Finalize that inventory after dependency/include discovery and before semantic resolution/admission. Each entry records its origin, exact source snapshot, effective validated file options, and independent compilation and verification roles. It includes:

1. Requested source and project roots, and their transitive includes.
2. Direct `.doo` arguments and `--library` `.doo` arguments.
3. Source files passed through `--library`.
4. Embedded standard-library resources and the selected target-specific standard library.
5. Other supported sources of retained Dafny text, including DLL `DafnySourceAttribute` loading and project dependency outputs.

A `.doo` does not bundle every separately compiled dependency: serialization omits library modules, and the manifest deliberately excludes the `Libraries` option. Resolve and check the dependencies actually supplied to the consumer. A missing dependency remains an error. Do not invent its source or accept its absent proof because some earlier producer had it. An unsupported source-bearing input handler must either participate in this inventory or fail with a specific option-on diagnostic; it cannot silently retain an “already verified” exemption. [Loading][dafny-file]; [serialization][printer]; [manifest][doo]; [project handling][new-cli].

Use immutable source snapshots through parsing, proof and compilation. Do not reread a changed file or archive after checking it and then execute the new bytes. The checked semantic program and the backend's selected implementation bindings must match. Independently supplied native libraries retain the ordinary trusted compiler/linker/extern correspondence premise; M4b does not verify arbitrary native code against retained Dafny text.

### 3.2 Producer and consumer options

The **consumer's** modular-termination option selects the guarantee:

| Producer | Consumer | Required behavior |
| --- | --- | --- |
| Off | Off | Existing behavior and output, unchanged. No modular-termination guarantee. |
| On | Off | Existing legacy interpretation and loading behavior. The producer's flag supplies no transferable guarantee. |
| Off | On | Parse the retained source with modular termination enabled, resolve the final composition, and recheck the complete closure. |
| On | On | The same complete consumer check. The earlier result does not omit any obligation. |

Keep `DooFile.Validate`'s existing Dafny-version and semantic-option compatibility checks. M4b does not relax an otherwise incompatible producer option. After successful validation, the consumer's verification policy governs scheduling: a producer's `hidden-no-verify`, old root role, cached `ShouldVerify` result or recorded solver version must not suppress a required check. If an option cannot safely be separated from source semantics, reject that combination with a rebuild diagnostic.

The consumer flag must be active **during parsing**, not first set after resolution: `GlobalDecreasesAttribute` moves expressions between ordinary and global/callback clauses according to that flag. Rechecking makes an off-produced artifact's reinterpretation legitimate; blindly trusting its old proof would not.

Under the option, reject an unsupported `DooFileVersion` before interpreting the archive. Continue accepting the known existing `1.0` layout. This is a reader check, not a writer or format change. The option-off reader follows its old path.

### 3.3 Verification roles must be separate from compilation roles

For complete verification, every loaded source declaration's owning proof is eligible, regardless of `AlreadyVerifiedRoots`, `--library`, archive origin or include origin. Keep all current compilation decisions. In particular, rechecking a `--library` input must not make the backend emit that library a second time.

Implement an explicit option-on verification-role policy in `CompilationData`/`ShouldCompileOrVerify`; do not make scattered mutations of the two existing exclusion flags. Invalidate cached `UrisToVerify` whenever that policy is constructed or changed. Symbol discovery and Boogie translation must consult the same policy.

Every function, method, constructor, iterator and proof-relevant synthetic declaration has either an owning set of verification parts or an explicitly identified ordinary Dafny assumption. Inventory specifications, body well-formedness, bodies, ordinary and modular termination, overrides, witnesses, constraints, defaults, constants, lambdas and by-method obligations. “This method was scheduled” is insufficient if the translator omitted its body.

### 3.4 Final semantic analysis

After resolution, selected replacements, refinement instantiation and all supported semantic postpasses, construct **one final modular-termination analysis**. Use that same immutable analysis for classification, bound-closure checks and translation. Do not leave `PostResolve` using a fresh analysis while `ModularTerminationAnalysis.Of` can return an earlier cached analysis for the same `Program`.

Recompute on every new resolution generation, including IDE module-cache hits. Reset admission before waiting for a cache lock or allowing cancellation. Do not clone or serialize admission. Current module cache entries can still accelerate valid local resolution; they cannot validate the final program's openness. A plugin that changes semantic declarations after admission invalidates it. Unsupported plugin transformations must fail closed for a complete checked result rather than leaving a stale graph.

Use the shared `ProgramResolver.Resolve` finalization point after the existing `PostResolve` loop, with explicit ordering relative to cardinality validation. Move or split the modular rewriter so diagnostics are emitted once from the final analysis. Both resolver engines must reach this point only after type inference, default-argument substitution and call-graph construction are finished. [Program resolution][resolver]; [rewriter ordering][rewriters]; [resolver caching][resolver-cache].

### 3.5 Transparent views, abstract contracts and refinements

Track three different identities:

| Kind | Identity and rule |
| --- | --- |
| Transparent export/visibility view | Same concrete owner/slot, plus the view's type substitution. Use the owner's internal effect information; preserve the view's logical visibility. |
| Abstract import contract | A distinct contract instance. Do not infer a hidden implementation by following `OriginalSignature`. An uninstantiated abstract import is not a checked concrete body. |
| Concrete refinement or selected replacement | A distinct semantic body realization with its actual bindings. Recheck inherited obligations and its relation to the advertised contract. Shared source tokens do not make it the base body. |

Record view provenance at clone construction, where the correspondence is known: `ScopeCloner`, `AbstractSignatureCloner`, and the manual abstract-signature cloning paths in `ModuleResolver`. The existing type-level `CardinalityViewOf` is a useful precedent, not a complete callable identity mechanism. Add member/constant provenance and distinguish the reason for cloning. Do not use qualified names, URIs, source spans, `CloneId`, compile names, or unconditional traversal of `RefinementBase` as proof identity.

The internal analysis may inspect an available concrete owner's body behind a transparent export without revealing that body as a logical definition to clients. It must not weaken export abstraction to make a proof succeed.

Replace the implicit “not in `reaching` means closed” convention with a total classification: **`Closed`, `OpenReaching`, or `UnresolvedContract`**. A declaration is `Closed` only after its relevant evaluation closure has been analyzed. A missing owner, erased body without provenance, unsupported semantic node or uninstantiated abstract dependency is not a closed leaf. Use this rule for direct calls, known function values, M4a rank dependencies, constants and type-evaluation nodes.

Propagate unresolved dependencies backwards through the same evaluation edges as open reachability. A helper that calls unresolved code is itself unresolved; visiting its own body does not make it closed. Compute SCCs/fixed points conservatively: an SCC is closed only when all its relevant outgoing dependencies are resolved closed and it contains no open or unknown seed. If both open and unresolved effects are reachable, retain both reasons internally even if the public classifier reports unresolved first. Neither state is eligible for M4a's closed-rank rule.

An unresolved contract can support an explicitly conditional abstract-module check under its advertised interface; it cannot discharge the concrete implementation's proof inventory. Where current M1–M4a lacks a rule for the dependent modular call, reject it rather than inventing closedness or a new axiom. A complete concrete composition must supply and check the chosen realization, or identify an ordinary trusted external contract. General inference for unknown abstract effects is outside M4b.

### 3.6 Inherited obligations must actually be checked

Freshly checking each library URI is not enough. The existing translator can change an assertion into an assumption because its token is inherited. M4b must not reuse an inherited proof whose openness, dependency bindings or global-bound interpretation has changed.

In the complete option-on path, force re-verification of each selected concrete realization's proof-relevant inherited parts, at the same semantic locations used for an ordinary non-inherited implementation. Prefer an explicit replay context over changing the general meaning of `RefinementOrigin`. The safe first implementation rechecks the inherited ordinary/body/specification obligations too; a later optimization may reuse them only with an exact proof-context argument. Preserve free caller-facing contracts, preconditions assumed at body entry, and intentional well-formedness branches. A bodyless facade/extern contract remains conditional/assumed; do not fabricate an implementation proof for it.

Specifically, ensure that checks emitted by `CheckModularTermination`, `CheckCallbackArguments`, `CheckCallbackApplication`, global override checks and their well-founded lower-bound side conditions are assertions. Wrap/check nested origins consistently if using `ForceCheckOrigin`. A single force-wrapped outer comparison is insufficient if a nested lower-bound obligation remains inherited and is suppressed. Do not globally alter `AssertMode` in proof contexts that intentionally assume facts; count any intentional suppression in the proof inventory instead of treating it as a discharged check.

Add a structural BPL test and a semantic rejection test for an inherited call with an inherited global clause, where only the selected import/foreign extension changes. Existing `CheckCallTermination` forces inherited calls when all context decreases expressions are new; that condition does not cover this case. [Assertion emission][boogie]; [decreases checks][decreases].

Audit direct inheritance handling as well as `Assert`/`AssertAndForget`. In `BoogieGenerator.Functions.Wellformedness.cs`, `FunctionWellformednessChecker.Check` omits checked inherited ensures. In `BoogieGenerator.Methods.cs`, `AddMethod`'s local `GetEnsures` guards inherited implementation postconditions with `$_reverifyPost`, which the body builder initializes to false. `BoogieGenerator.Obligations.cs:CheckExitPostconditions` also guards or skips inherited postconditions and their well-formedness work. `BoogieGenerator.Iterators.cs:AddIteratorProc` omits inherited implementation ensures, and `AddIteratorImpl` uses the non-reverification exit-check path. A replay context must cover these producers under both settings of `ConsistentObligationChecks`. Wrapping only a call-site origin does not restore their missing proofs.

### 3.7 Admission is distinct from completed verification

Use two nonserialized, per-resolution objects. Proposed names and conceptual fields are:

```text
ModularTerminationAdmission
  policy = source-recheck-v1
  program identity and resolution generation
  immutable inputs and effective semantic options
  selected replacement / view / refinement bindings
  final effect analysis
  required owning declarations and proof-part inventory
  explicit trust dependencies and unsupported/partial conditions

ModularTerminationCompletion
  exact admission identity
  actual verifier outcomes for the required proof parts
  coverage: complete / partial / failed
  remaining explicit trust dependencies
```

Admission permits translation; it is not evidence that the program verifies. Completion is created only after matching the full required inventory to successful outcomes. Empty task lists, zero reported errors, successful resolution, a cache lookup, or an archive's existence are not completion evidence. Canceled, missing, timed-out, resource-exhausted, omitted and filtered parts never count as `Correct`.

Admission inventories owning declarations and required obligation producers independently of the eventual task list. Translation must report completion for each required owner, with either its full emitted-obligation manifest or an explicit legitimate zero-obligation outcome. Construct that manifest before selection filters and record any inherited/body suppression separately; do not infer expected coverage from an already pruned or filtered result. Boogie preparation then expands emitted implementations into the complete split-task inventory, again before task filters. Completion checks those inventories and rejects omitted required bodies/producers. Exact split-task identities need not exist at resolver-admission time. A genuinely empty proof inventory can complete after these coverage checks; an accidentally empty task list cannot establish that it was genuinely empty.

An ordinary unfiltered `verify --modular-termination`, and a normally verified `build`/`run` under the option, must require complete coverage, conditional only on the enumerated ordinary assumptions. Unexpected partiality is an error naming the omitted owner/part. A source/library `@VerifyOnly`, `{:only}` or hidden suppression must not silently downgrade that request to a successful partial check.

Explicitly requested CLI/editor partial checks can remain useful: `--filter-symbol`, `--filter-position`, assertion filters and Boogie implementation filters select such a request. Their status must explicitly remain partial, and they cannot create a complete-program result or authorize a checked build. Source selection annotations encountered by a full-check request instead receive the coverage error above. An explicitly unverified command remains unverified and supplies no modular-termination guarantee or receipt.

Treat `{:verify false}`, bodiless assumed lemmas, `{:axiom}`, externally implemented contracts and similar existing trust features explicitly. They cannot masquerade as proof parts that ran. A result with such dependencies is conditional on those assumptions. `decreases *` and productive corecursion retain their ordinary meanings; this work does not prove that deliberately nonterminating methods terminate. The source/library exclusion introduced by loading a `.doo` is **not** an admissible additional trust assumption in a complete option-on run.

The two cross-module extension attributes alone are not a reason to skip the modular obligations. The point of M4b is to check the obligations that their later use makes necessary.

## 4. Boundary invariant and soundness argument

### 4.1 Statement

Let `P` be an immutable, fully resolved composition: requested roots, retained library sources, their actual dependencies, transparent views, and selected concrete refinements/replacements. Let `A` be its explicit ordinary Dafny trust assumptions. Let `O(P)` be the final open-trait/slot set and `R(P)` the conservative evaluation closure reaching open dispatch or the unknown applications covered by M3.

Let `Joint(P)` mean a complete verification of the same semantic composition as source, with M1–M4a enabled and the identity/coverage corrections in section 3. The required M4b invariant is:

1. **Coverage:** every proof-relevant concrete declaration/evaluation in `P` has all required parts checked in this generation, or an explicit assumption in `A`. No earlier library result is used to remove a part.
2. **One environment:** every such check uses this `P`, its effective options, `O(P)` and `R(P)`. The compilation bindings match it.
3. **Independent measures:** every global and callback bound passes M4a against the full final closure; missing/abstract/erased code is not silently closed.
4. **Identity fidelity:** transparent views preserve owner/slot identity and substitutions, while abstract contracts and different concrete realizations are not collapsed.
5. **Proof completion:** every required check is actually asserted and discharged; inherited-origin suppression, filters and cached results do not fabricate coverage.

The boundary theorem is:

```text
CompleteM4b(P, A)  implies  Joint(P, A).
```

Consequently, **assuming the soundness of the existing closed-code verifier and the joint M1–M4a rules**, complete M4b verification establishes the same termination and logical soundness guarantee for `P`, conditional on `A`. This is a reduction of separate verification to joint checking, not a new proof of Dafny, Boogie or Z3.

### 4.2 Why the rules maintain the invariant

**Loading.** Exact retained source and actual dependency snapshots define `P`; a file's producer, age, filename and previous proof do not discharge an obligation. Input validation rejects unsupported interpretation rather than providing a success fallback.

**Resolution.** Complete source closure and final binding analysis identify every concrete owner. Transparent views refer to that owner's effect, not to an erased empty body. A distinct refinement is checked as a distinct realization. Final admission occurs after semantic postpasses, so there is one environment for all later checks.

**Checking.** Restored proof roles expose the same owning declarations and generated parts that joint checking requires. The replay context prevents inherited proofs from suppressing newly needed checks. Verification tasks consume the admitted final analysis. Completion checks inventory coverage, so omission is a failure or a partial result, never an implicit proof.

**Compilation.** A checked build uses the same snapshots, semantic options and selected implementations. Leaving compilation roles unchanged avoids replacing a library or compiling another copy merely because its proof was repeated.

Together these establish `Joint(P, A)`. The library boundary introduces no assumed fact or axiom, and no alternative call ordering. It adds proofs that were previously omitted and rejects missing evidence.

### 4.3 Relation to the termination argument

In the bounded open-call fragment, M4a gives well-defined rank values independently of the open recursion. M1 strictly decreases the outer bound at open dispatch; M2 prevents forwarding helpers from increasing it; M3 covers its admitted callbacks and implicit evaluations. An infinite chain cannot have infinitely many strict outer descents. After those descents stop, the existing local/module/SCC/decreases discipline excludes an infinite remaining chain; the usual loop checks exclude divergence between calls.

Closed sinks and higher-order uses governed by the existing function-value restrictions retain those existing proof premises. In particular, the argument does **not** pretend that every TOP-to-TOP function-value application has a strict global decrease. Its correctness is inherited from the joint checker, including its conservative M3 rules. The tests must retain the M4a specification/fuel/vacuity cases; replay is not a substitute for their logical soundness.

### 4.4 The four required boundary cases

**Library verified alone, then extended.** Let its producer have checked `P0`, and let the consumer compose `P1 = P0 + C + E`. M4b ignores completion of `P0`, computes `O(P1)` and `R(P1)`, and checks all of `P1`. If an extension makes `H`'s call require a new strict inequality, that inequality is checked in `H`. A failing extension is rejected. Formally, for every statically assembled extension `E`, acceptance of the new complete check implies `Joint(P1, A)`; arbitrary unverified runtime loading is outside this model.

**Different options.** Consumer-on reparsing and full proof use the validated current semantics. No result proved with the producer's ordinary-decreases interpretation is substituted for a global-bound check. Incompatible language options are rejected. Consumer-off remains legacy and has no M4b theorem, including when the producer used the option.

**Refinement and exports.** The owner/view distinction prevents erased bodies from becoming closed leaves. The realization distinction prevents a proof of one substitution from justifying another. Forced inherited checks prevent origin-token reuse from turning new obligations into assumptions. Abstract interfaces prove conditional contracts until the actual implementation is checked.

**Old or changed artifacts.** An old `.doo` is an old source snapshot. Rechecking it with its current dependencies establishes a theorem about that snapshot. Changed embedded source, contracts, options or dependencies create a different `P` and require new admission and proof. There is no stale persistent M4b certificate to accept. This does not establish freshness relative to a newer source tree that the consumer did not supply.

## 5. Alternatives: soundness, completeness, annotations and cost

| Alternative | Soundness condition | Accepted terminating programs / annotations | `.doo` impact | Cost and recommendation |
| --- | --- | --- | --- | --- |
| **A1. Recheck only newly openness-dependent VCs** | Must identify the entire logical proof dependency closure, including bounds, specs, `canCall`, ordinary termination, implicit evaluations and inherited assumptions. | Potentially the same acceptance as full replay, with no extra source annotations. Missing a dependency makes it unsound. | None required, but reliable old-proof provenance is still needed to justify omitted proofs. | Less solver work if an exact dependency argument exists; substantially harder to establish first. Do not ship as the initial shortcut. |
| **A2. Full consumer source replay — chosen** | Exact final composition, complete proof coverage and section 3 identity rules. | Matches joint source checking within supported/trusted semantics. Does not impose a new syntactic rank scheme or universally open traits. A newly open composition can need the existing M1–M3 annotations or be rejected. | **No schema, manifest or writer change.** | Repeats dependency proof work. Smallest new proof/trust boundary, with an executable joint-source oracle. |
| **B. Manifest summaries/certificates** | Each summary must be a proved theorem tied to code, bindings, options, verification policy and trusted producer evidence; consumer validates applicability. A hash or `verified=true` is not a proof. | Can preserve closed proofs in unchanged worlds and open proofs across conforming extensions. No necessary extra syntax if inference succeeds; current helper inference is absent. | Versioned on-only section, producer completion plumbing, dependency identities, invalidation, old-reader policy and a trust/proof-checking mechanism. | Fast reuse, largest implementation/compatibility surface. Defer until there is measured need and a reviewed evidence protocol. |
| **C1. Forbid late extension of a library trait** | Reliably enforce sealing across aliases, exports, refinements and all artifact-loading paths. | Rejects terminating late extensions, including harmless leaf implementations. Few annotations, significant loss of composition. | Needs an enforceable sealed/open contract or a conservative consumer prohibition. | Cheap but too restrictive as the general solution. May be an explicit future library policy. |
| **C2. Permit late extension only when a bound/contract is declared** | Must also prove the producer's callers against that contract. Checking the override or presence of a bound alone is insufficient. | Supports ranked cycles when all forwarding code satisfies its contracts; current helpers may need explicit bounds. Universal-open producer checking rejects some unchanged closed libraries. | Requires durable contract identity/evidence, or replay. | Useful as part of genuine open certification. **Alone it fails this document's reproducer: the trait and helper already declare bounds.** |
| **D. Full replay plus exact consumer-result caching** | Fresh final admission, exact complete proof-context key, trusted local results or independently checked evidence, and complete coverage. | Same logical acceptance as A2; cache eligibility may conservatively fall back to fresh checking. | No `.doo` change; separate cache storage. | Best subsequent performance direction. Reuse #241 rather than building a second cache. |

Universal-open producer checking is a possible implementation of B/C2, but it has a real completeness cost. For example, a library can contain same-module implementations and a closed helper that terminates under the existing local ordering. Its unextended consumer need not add global bounds. A2 preserves that possibility. A stable open contract would instead have to work for unknown implementations, often requiring new bounds on the trait and helpers.

No alternative makes every semantically terminating program automatically provable. “Completeness” here is relative to available Dafny ranks/contracts and solver proofs. Full replay also does not guarantee identical on-mode solver outcomes to producer verification: dependencies and proof obligations have changed, and time/resource limits may expose failures. The census must separate these from logical annotation requirements.

For A2, **new syntax required by M4b is zero**. Newly exposed termination-contract requirements use existing `{:global}` or `{:callback}` clauses. Full ordinary replay can also expose old proof failures that require existing proof annotations or repairs, or fail because of solver limits; count those separately. There is no defensible numeric estimate of newly rejected programs before implementing replay and running the composition-aware census. Earlier M4a zero-rejection numbers do not answer this question.

## 6. Stored data, versions and invalidation

### 6.1 What M4b stores

There is **no new durable M4b data in `.doo`**. Keep `manifest.toml`, `program`, option registration, serialization, entry order and ZIP writing unchanged. In particular, do not re-register `ModularTermination` as a global/module option merely to record it: that would serialize a new `false` field into option-off files.

The admission/completion objects are process-local metadata, explicitly excluded from cloning and serialization. `source-recheck-v1` identifies the checking policy for diagnostics and any future cache descriptor; it is not a claim that the old `.doo` format contains a certificate. Declaration IDs need only be valid within this resolution generation. Cross-process stable IDs are deliberately not a new M4b dependency.

### 6.2 Invalidation rules

| Change | M4b behavior | Requirement for any future result reuse |
| --- | --- | --- |
| Any loaded source/program text, body, spec, rank, callback contract, type constraint, witness, default or constant | New snapshot, resolution and verification | Include exact relevant content; initially key the whole composition conservatively. |
| New foreign subtype or changed imports/exports/refinements/replacement bindings | Recompute identities, openness, reachability and all proofs | Include the final binding/effect context, not just the edited module or import DAG. |
| Producer modular-termination flag/result | Never accepted as proof evidence | Producer result cannot substitute for consumer completion. |
| Consumer modular flag, resolver, language semantics, verification scope, filters or assumptions | Fresh policy/analysis/proof; off path stays legacy | Key all effective values and coverage; partial/assumed is not unconditional complete success. |
| Dafny/Boogie build, prelude, plugins or generated obligation policy | Fresh current check | Use actual tool/content identities; the assembly version string is insufficient. |
| Solver binary/version/options, limits, seeds or splitting policy | Fresh current proof | Include actual solver identity and effective configuration; follow #241's outcome-equivalence eligibility. |
| Cancellation, resolution error, changed AST after admission or unsupported transformation | No completion; discard stale admission | Never turn an incomplete result into a cache hit. |
| External source tree changes while the selected `.doo` remains unchanged | Verify the selected older snapshot | A freshness check needs separately supplied build provenance; do not invent it. |

Do not “invalidate” an archive merely because it was checked with a different solver: no producer proof is being reused. Existing format/language compatibility checks still apply, and the current solver establishes the current result. Likewise, a changed producer source tree is not detectable from a portable archive that contains only its earlier source.

### 6.3 Contract with #241

The [#241 study][cache-study] proposes exact task descriptors and persistent result reuse; it is not an existing proof service. M4b supplies the current proof inventory and semantic admission. Cache lookup, if subsequently enabled, occurs **after** that admission and after the current required parts are known.

A conservative cache key must bind the entire ordered proof context, selected input snapshots, effective options, view/refinement/replacement environment, prelude, Dafny/Boogie/solver identities, obligation encoding and verification scope. Cache a successful actual result, not a claimed manifest flag. A hash establishes association with data, not that verification occurred; an untrusted imported record cannot be treated like a local verified result merely because its digest matches.

Initially use a whole-composition dependency digest, even if an unrelated new extension causes a miss. Dependency-sensitive reuse and portable open certificates require separate arguments. Never let a proof cache hit bypass current M4a closure, openness classification, cardinality admission or completeness checking. Preserve #241's distinct requirement about verdict/resource equivalence; a sound old theorem alone does not establish equivalence of a fresh timed solver run.

## 7. Required code changes

These are implementation instructions against the pinned tree. New filenames below are proposals. No AST, proof or format change has been implemented by this document.

| Area and existing symbols | Required change |
| --- | --- |
| `Source/DafnyCore/DafnyFile.cs`: `HandleDooFile`, source/DLL handlers | Retain source origin, validated effective options and input kind; route every retained-source library through the option-on verification policy. Preserve `ShouldNotCompile`. Reject unsupported option-on input/schema cases explicitly. |
| `Source/DafnyCore/DooFile.cs`: `Validate`, `CheckAndGetLibraryOptions` | Keep existing compatibility checks; add the option-on known-format check; prevent producer proof-suppression options from excluding replay. No change to `ManifestData` construction or serialization. |
| `Source/DafnyCore/AST/Grammar/ProgramParser.cs`: `ParseFiles`; `AST/CompilationData.cs`; `AST/ShouldCompileOrVerify.cs` | Carry independent source/proof roles; compute complete option-on verification closure across roots/includes/library inputs; invalidate scope caches. Preserve the off path and all compilation roles. |
| `Source/DafnyCore/AST/Modules/ScopeCloner.cs`, `AbstractSignatureCloner.cs`; `Resolver/ModuleResolver.cs` abstract-signature paths; `Rewriters/RefinementTransformer.cs` | Capture member/type/constant view provenance and substitutions; separate abstract contracts from pure views and semantic realizations. Reuse existing correspondences where valid, without equating refinement ancestry to identity. |
| **New:** `Source/DafnyCore/Resolver/ModularTerminationIdentity.cs` | Own current-resolution declaration/view/realization identity and explicit unknown handling. No durable nominal-name certificate keys. |
| `Source/DafnyCore/Resolver/ModularTermination.cs` | Make effect classification total; apply M4a/classifier checks to the complete proof closure; use canonical transparent identities and actual realization bindings. Supply one final analysis; invalidate/remove the current program-only lazy analysis reuse. |
| `Source/DafnyCore/Resolver/ProgramResolver.cs`; `Rewriters/RewriterCollection.cs`; `AST/Program.cs` | Reset, build and store nonserialized final admission after semantic postpasses. Keep cardinality validation. Ensure both resolver paths and cached-module paths reach final admission. |
| **New:** `Source/DafnyCore/Resolver/ModularTerminationAdmission.cs`; **new:** `Source/DafnyCore/Verifier/ModularTerminationCompletion.cs` | Define the policy, required declaration/proof-part inventory, lifecycle and completion aggregation. Inventory distinguishes checked, assumed, filtered, unsupported and failed parts. |
| `Source/DafnyCore/Pipeline/TextDocumentLoader.cs`: `ResolveInternal`; `Pipeline/Compilation.cs`: `VerifyCanVerify` and task scheduling; `Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs`: `GetVerificationTasksAsync` | Discover/schedule owning library proofs; require current admission for translation; associate all generated parts and outcomes with the inventory. Pure facade copies do not need duplicate bodies, but must map to a checked owner or conditional contract. |
| `Source/DafnyCore/Verifier/BoogieGenerator.cs`: `ShouldVerifyModule`, `InVerificationScope`, `Assert`, `AssertAndForget` | Honor complete verification scope and a replay context for inherited obligations. Add a modular admission guard analogous in lifetime to `RequireCardinalityValidation`. Do not reveal private definitions or weaken normal visibility. |
| `Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs`: `CheckModularTermination`, callback checks, `CheckCallTermination`; `BoogieGenerator.Methods.cs` override/body paths | Ensure all required bound comparisons and lower-bound checks remain assertions; force complete concrete-realization replay where earlier inheritance would suppress proof. Detect source-level body omission as a trust/coverage condition. Audit function well-formedness/by-method/implicit-evaluation callers for the same context. |
| `Source/DafnyCore/Verifier/BoogieGenerator.Functions.Wellformedness.cs`: `FunctionWellformednessChecker.Check`; `BoogieGenerator.Methods.cs`: `GetEnsures` and body builder; `BoogieGenerator.Obligations.cs`: `CheckExitPostconditions`; `BoogieGenerator.Iterators.cs`: `AddIteratorProc`/`AddIteratorImpl` | Replay inherited concrete implementation postconditions/WF checks, including direct skips and `$_reverifyPost` guards, without changing caller-facing free contracts or inventing bodies for abstract declarations. Cover both consistent-obligation modes. |
| `Source/DafnyDriver/CliCompilation.cs`: selection and completion; `Commands/VerifyCommand.cs`; `Legacy/SynchronousCliCompilation.cs` | Aggregate actual proof coverage; normal unfiltered verification and checked builds require complete coverage. Explicit partial checks stay distinct. Exercise both pipeline entry paths that support the mode; do not add unsupported legacy-CLI `.doo` loading. |
| `Source/DafnyDriver/DafnyNewCli.cs`: project dependency handling; `Backends/Library/LibraryBackend.cs` | Dependency build success is not consumer proof success. Serialize the same source as today; serialize no completion object. Explicit no-verification builds confer no proof on their outputs. |
| `Source/DafnyLanguageServer/Language/CachingResolver.cs`, parser/resolver option keys | Invalidate admission per resolution and mode change; do not accept cached identities/effects from another replacement or openness context. Selected editor verification remains partial. |
| `Source/DafnyDriver/Commands/AuditCommand.cs`; `Source/DafnyCore/Auditor/AuditReport.cs`; auditor rewriter | Add option-on library/trust/coverage reporting after semantic admission. Audit is resolution-only and cannot report that it proved termination. Include imported assumptions; preserve off-mode report bytes and the separate warning policy. |
| `docs/DafnyRef/Specifications.md` §7.1.3.1; CLI help; `docs/dev/news/`; `.github/review/` | Document consumer replay, mixed-mode behavior, conditional/partial results and cost; remove the library-openness caveat only after the full fix passes. Follow product-PR base/regression/hash and verdict-update rules. |

The translator-related changes only restore/force checking and reject missing proof evidence. They introduce no axiom or new assumed ordering fact. Review any implementation departure from that property explicitly under the fork's added-axiom policy.

### Audit behavior

`dafny audit` sets `Verify = false`, and the current auditor runs before the modular rewriter. Its report also excludes included declarations in places. Under the option, report the loaded library snapshots, relevant extension attributes, ordinary assumptions, unresolved contracts and the need for full replay after final admission. Say **“not verified by this audit run”**; a structural check does not certify totality. Do not remove existing assumption findings merely because modular checks would be generated in a different command.

### Standard libraries

Embedded and target-specific standard libraries follow the same rules. There is no trusted “official library” exception. Verify their retained source with validated semantics, including the transitive source/target configuration selected by the consumer. Preserve existing backend resource and compilation selection. If this exposes failures, record the exact declaration and obligation; do not silently exempt the library or rewrite expected verdicts to success.

## 8. Regression and validation plan

### 8.1 Harness

Add a driver `Source/IntegrationTests/TestFiles/LitTests/LitTest/doofiles/modular-termination.dfy`, matching `.expect`, and inputs under `doofiles/Inputs/211-m4b/`. Add a small forwarding regression entry to the existing #211 git-issue driver if needed for the fork's regression index. Use the repository's `dotnet test` integration runner and its `%dafny` substitutions; do not substitute an unrelated external LIT setup.

Run semantic families under both resolver engines. Build producer artifacts inside each test where the producer option matters; do not check in a supposedly verified fixture whose origin cannot be reproduced. Keep all M1–M4a tests, including the rank-through-specification and fuel controls.

For every semantic rejection family, include a client that attempts a reachable `assert false`. Require the intended resolution/termination failure, not a timeout, malformed input, crash or unrelated option error. A resolver rejection prevents assertion verification; do not claim its assertion VC ran. For every positive family, add a reachable false companion which must fail verification. In malformed/version/partial-coverage cases the false companion must not obtain a **complete** checked result; never count “zero tasks, zero errors” as a passing control.

### 8.2 Required cases

| IDs | Case and required result with consumer on |
| --- | --- |
| `mode-1`–`mode-4` | All four producer/consumer flag combinations. Consumers off match the baseline; consumers on recheck both producer kinds. Run the harmless library and the executed false-proving library. |
| `late-1` | The exact reproducer above: both `.doo` producer modes reject at the library's newly open strict bound check. |
| `late-2` | A genuinely decreasing variant, with guards at zero and strict descent at the dispatch: accepted; false companion rejected. |
| `late-3` | Unextended, closed library with same-module implementations and no new bounds: accepted if joint source checking accepts it. No universal-open seeding. |
| `route-1`–`route-5` | Repeat the late-extension family through direct `.doo`, `--library` `.doo`, source `--library`, transitive dependency archives, and project dependency loading. Include ordinary transitive includes. |
| `route-6` | Retained source in DLL input, when supported: same coverage; otherwise an explicit supported-scope rejection, never silent old-proof trust. |
| `rank-1` | A library rank helper is closed at producer time but open in the consumer: M4a rejects the inherited or declared bound through that helper. |
| `rank-2` | The same transition through a constant initializer, type constraint, witness or default value: rejected by the intended M3/M4a route. |
| `rank-3` | A closed recursive rank helper behind a transparent export: accepted with its actual owner checked; false companion rejected. |
| `callback-1` | Library applies a bounded callback and consumer supplies a valid smaller lambda: accepted; equal/increasing callback cycle rejected. |
| `callback-2` | A named function hidden by a facade reaches open dispatch: cannot become `KnownCode` because its visible body is absent. |
| `view-1` | Same trait/member through two export aliases and different export sets: one slot/effect identity with correct substitutions. |
| `view-2` | `provides` versus `reveals` for a helper, method or constant: hiding cannot erase its effect or make the dangerous cycle verify. |
| `view-3` | Generic trait instantiations and nested facades: correct type substitutions, with same-spelled distinct declarations kept distinct. |
| `abstract-1` | Abstract import checked as a contract, then realized by safe/unsafe implementations: only the safe complete realization receives completion. |
| `refine-1` | Inherited helper, inherited global clause and inherited call; consumer/replacement makes the trait open: reject the new failed obligation. Inspect BPL for `assert`, including lower-bound checks. |
| `refine-2` | Two refinements with identical inherited source locations but different imports/body bindings: no receipt/effect aliasing. |
| `refine-3` | Safe empty refinement and safe selected replacement: accepted; false companions rejected. |
| `stale-1` | Keep an old `.doo`, edit external original source: result describes the loaded old snapshot, not the newer unprovided source. |
| `stale-2` | Change embedded source, a bound or a supplied dependency while retaining old manifest/version strings: fresh check sees the changed behavior and rejects the dangerous variant. |
| `stale-3` | Replace an implementation/export/refinement binding without changing the client source: final graph and inherited proofs are rechecked. |
| `format-1` | Unsupported schema, missing/truncated entries and malformed manifest: explicit failure, no proof success. Legacy option-off behavior remains baseline. |
| `option-1` | Same public Dafny version but different producer build; different producer solver: current fresh proofs run, rather than trusting those version strings. Preserve genuine language-option incompatibility errors. |
| `option-2` | Producer `--hidden-no-verify`/other suppression: cannot suppress consumer replay. Existing incompatible `--no-verify` artifacts may still fail compatibility before proof. |
| `coverage-1` | Explicit symbol/position/assertion filters and no-verification modes: no complete receipt; checked build cannot use a partial result. Hidden library `@VerifyOnly`/`{:only}` must make an unfiltered full-check request fail for incomplete coverage. |
| `coverage-2` | `{:verify false}`, externs, axioms, abstract bodies and `decreases *`: explicit conditional/partial status; no unconditional totality assertion. |
| `coverage-3` | A fully translated trivial owner with genuinely zero obligations can complete; an owner lost through filtering, body suppression or an empty missing-task lookup cannot. |
| `cache-1` | IDE keeps an upstream module cache hit while a downstream extension changes: final openness and library proof inventory change. |
| `cache-2` | Repeat resolution of a `Program`, cancel before/after cache locking, and flip modular/resolver options: no stale admission or `ConditionalWeakTable` analysis. |
| `cache-3` | If #241 is present, stale/malformed/mismatched results and changed solver/prelude/options fall back or fail; hits still perform current admission and coverage checks. |
| `std-1` | Standard-library source project, embedded `--standard-libraries`, and every target configuration: same rule, no exemption. |
| `backend-1` | Positive compile/run using a checked library: no duplicate generated implementation, unchanged selected target code, successful ordinary computation. |
| `order-1` | Supported postpass changes a relevant semantic edge: final admission sees it; unsupported late mutation cannot retain completion. |

Where a family needs several inputs or modes, give each actual invocation its own expected outcome in the implementation PR. The IDs are coverage requirements, not a claim that a specific number of solver checks has already run.

### 8.3 Census of newly rejected existing programs

The existing `.github/review/modular-termination-census.py` is a **resolution/classification census**. It resolves individual files and counts hints; it does not execute library build/consume RUN sequences or prove their VCs. Retain it as a static inventory and add a composition-aware verifier comparison. [Census script][census]; [workflow][census-workflow].

Measure four comparisons separately:

| Comparison | Question |
| --- | --- |
| Base on versus candidate on | Which existing invocations are newly rejected specifically by M4b? |
| Base off versus candidate off | Is the default path byte/verdict/resource identical under controlled inputs? |
| Candidate off versus candidate on | What is the full adoption cost, including earlier milestones? |
| Candidate source-composition versus candidate library-composition | Do the library roles omit or alter required checking? |

Use real LitTest command/configuration sequences, not an unconditional flag appended to every command regardless of whether it is supported. Include generated archives and separately loaded dependencies. For standard libraries, use the existing main-project and six target-configuration verifier jobs, including those using precompiled binaries. Unsupported mode/configuration combinations must be counted separately, not silently dropped.

Record per invocation: base/candidate revisions, actual command and options, producer/consumer modes, input and artifact identities, resolver result, verifier verdict, intended changed obligation, rechecked declarations/parts, elapsed time, peak memory and any reuse. Distinguish expected-negative tests, newly exposed library bugs, missing existing bounds, incompatible producer semantics, unsupported identities, solver failures and time/resource failures. Report program/invocation counts and distinct declarations needing edits; diagnostic counts are not annotation counts.

**Current measurement status:** the targeted base experiments in section 2 are complete. The M4b newly-rejected census is **not measured**, because this PR is a design. Publishing that census, with every new rejection explained, is a merge gate for the implementation. Do not reuse the M4a zero-rejection result as an M4b forecast.

### 8.4 Default byte identity, including `.doo`

With the option off, execute the unchanged loader, parser, resolver, proof-scope, translator, audit and writer paths. Compare base and candidate diagnostics, BPL/SMT output, verdicts and deterministic resource counts using the fork's established harness. No new manifest key, source marker, certificate entry, option default, archive entry or release-version side effect is allowed in this comparison.

Compare **raw `.doo` bytes**, not just extracted entries. The existing writer uses ZIP/file timestamps: two uncontrolled builds of the base can already differ. Run base and candidate with the same supported test version, inputs, paths, compression environment and controlled clock/file metadata before archive creation. Compare raw output, entry order, compression and metadata; extracted payload comparison is a useful additional diagnostic. Do not normalize differing archives after creation and call them byte-identical, and do not change the off-mode writer to simplify the test.

The release's deliberate product-version change is a separate packaging input. Hold that input fixed for the behavioral identity test, and record actual release metadata separately. Existing wall-clock nondeterminism is not a reason to tolerate changed substantive bytes.

## 9. Cost and completeness limits

Let `Vroot` be current root proof cost, `Vlib` the cost of verifying the complete loaded library closure, and `I` the added provenance/inventory cost. The cold consumer cost is approximately:

```text
current parse/resolve cost + Vroot + Vlib + I
```

The `.doo` path already reparses/resolves retained text; the major added work is `Vlib`, not a new need to recover missing bodies. Final graph/provenance work should be linear in its explicit nodes and edges apart from existing resolution and substitutions. Solver cost is neither bounded by that graph size nor assumed negligible. Standard-library replay may dominate small consumers. Measure it before proposing a speedup or automatically enabling the option.

Full replay avoids an independent certificate checker, producer authentication policy, durable declaration identifiers and format migration. It also gives a direct test oracle: supplying the same concrete source together should not omit obligations that supplying it as libraries omits. The oracle still needs explicit scope/identity checks; merely comparing final Boolean outcomes could miss vacuous proofs in both paths.

The first implementation deliberately forgoes incremental proof reuse, early translation before final admission, and finer dependency slicing. Cache optimization should follow measured costs and #241's proof-context requirements.

Remaining expressiveness limits include all documented M1–M4a limits: no helper-bound inference; no bound evidence through arbitrary heap/collection/returned function values; no global bounds on types/constants/iterators; and no rank computed through dispatch. An unavailable abstract effect may need rejection even for a terminating implementation until its concrete realization is supplied. M4b does not silently solve any of these by trusting library metadata.

## 10. Milestones, one PR each

| PR | Work and exit criterion |
| --- | --- |
| **D — this design** | Review the invariant, selected A2 policy, executable baseline reproducer, mixed-mode table, exact code map and acceptance tests. Product behavior is unchanged; no release/fix claim. |
| **M4b.1 — provenance and final admission foundation** | Implement input-role inventory, pure-view/contract/realization identity, total effect status and generation-bound final analysis behind the existing option. Add structural and semantic identity tests. Preserve the manual's library-openness warning: complete consumer replay is not yet delivered. No new acceptance based on a receipt. |
| **M4b.2 — complete consumer replay** | Restore all source-library verification roles, inherited checks, task coverage and checked-build completion together. Include source/DLL/Std/project routes, both resolvers, audit/manual/help changes, release note, adversarial LitTests, full census and raw option-off identity gates in this PR. Only this complete increment closes library openness. |
| **Optional later PR coordinated with #241** | Reuse exact consumer results without changing admission, obligations, proof coverage or default behavior. Start with conservative complete-context keys and fresh fallback. This is not required to close M4b. |

M4b.1 is a preparatory change, not a claim that partial wiring makes the option sound. Do not split M4b.2 so that a loader begins trusting a producer certificate before all checking and coverage paths exist. Product PRs update `.github/review/base`, `.github/review/regressions`, applicable obligation-producer hashes and release notes as required by `AGENTS.md`/`REVIEW.md`; intended verdict-baseline changes follow the existing separate-commit procedure with a reason per change. This design-only PR does not update those product gates.

## 11. Limitations and questions to settle during review

1. **The proof is relative.** Boundary coverage has a concrete invariant and reduction argument; a machine-checked proof of M1–M4a and Dafny's logical encoding is not supplied. The implementation must still defeat the specification, fuel, callback, facade and refinement vacuity controls.
2. **On-mode cost is unresolved.** Full standard-library replay may be expensive. Measure it; do not reintroduce an unchecked exemption to make a benchmark pass.
3. **Abstract interface support needs conservative boundaries.** The implementation must enumerate supported contract/realization routes. Missing provenance or an unknown effect is a rejection/conditional result, never an inferred closed body. A broader abstract-effect language is separate work.
4. **Snapshot soundness is not source-tree freshness.** Without externally supplied build provenance, the consumer cannot know that an old `.doo` should have been rebuilt. It can prove the selected snapshot. A manifest dependency/freshness protocol would be a separately justified feature.
5. **Native linkage remains a premise.** The proof concerns the Dafny implementations represented by retained source and the selected realization map. Arbitrary separately supplied target binaries and extern implementations are not proved by inspecting that source.
6. **No guarantee for off or deliberately partial/unverified consumers.** This follows from opt-in compatibility. On-produced archives carry no guarantee that an off consumer can silently inherit.
7. **Upstream maintenance cost is concentrated in lifecycle integration.** Source loading, verification-scope filters, clone provenance, refinement assertion reuse, rewriter ordering and task scheduling are all touched. Keep the admission/identity policy centralized and leave grammar, `.doo` writing and backends unchanged where possible. Pin tests to these semantic boundaries rather than brittle line numbers.
8. **Portable open certificates remain a different deliverable.** They require a precise contract language, completeness/proof-status rules, dependency and toolchain identity, and a trusted-result or proof-checking story. This design explicitly defers that work while providing a sound uncached reference behavior.

## Source index

All implementation references below are pinned to the reviewed commit. The design was checked against the original attachment, the listed reviews, and the current reference-manual rules. The #241 study is linked at its own reviewed commit because it is a separate proposed change.

[request]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6780701809
[original]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6747601070
[original-review]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6747644412
[m4a]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6766513979
[m4a-approval]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6767272358
[release]: https://github.com/erniecohen/dafny/releases/tag/v4.11.0%2Bfcb2042d.review.06a2267a
[cache-study]: https://github.com/erniecohen/dafny/blob/435909156ed7e3ac407bf6526873e38c33643252/docs/dev/design/241-pipeline.md
[spec]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/docs/DafnyRef/Specifications.md
[doo]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/DooFile.cs
[library-backend]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Backends/Library/LibraryBackend.cs
[dafny-file]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/DafnyFile.cs
[program-parser]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/AST/Grammar/ProgramParser.cs
[scope]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/AST/ShouldCompileOrVerify.cs
[mt]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Resolver/ModularTermination.cs
[loader]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Pipeline/TextDocumentLoader.cs
[compilation]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Pipeline/Compilation.cs
[boogie]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Verifier/BoogieGenerator.cs
[decreases]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs
[scope-cloner]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/AST/Modules/ScopeCloner.cs
[options]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Options/CommonOptionBag.cs
[parser]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/AST/Grammar/ParserNonGeneratedPart.cs
[printer]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/AST/Grammar/Printer/Printer.cs
[new-cli]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyDriver/DafnyNewCli.cs
[resolver]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Resolver/ProgramResolver.cs
[rewriters]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyCore/Rewriters/RewriterCollection.cs
[resolver-cache]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/Source/DafnyLanguageServer/Language/CachingResolver.cs
[census]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/.github/review/modular-termination-census.py
[census-workflow]: https://github.com/erniecohen/dafny/blob/d0301c6f702dabada677d99c3ebf63745f357d9c/.github/workflows/issue211-census.yml
