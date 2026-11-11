# #211 M4b: declared openness and reusable modular termination contracts

**Status:** revised design for review; this PR changes no product code. It replaces the A2 recommendation in [PR #244][pr244] in response to [the latest request][request].

**Target:** `review/4.11.0`, inspected at `5f8855a4d9314e37f87d9900682ad71cc8434a55`. This revision incorporates M1–M4a, the review of the previous design, and the separate work in [#241][cache] and [#245][orders].

**Decision:** declare openness in the defining module, verify a producer against stable imported contracts, and admit its checked `.doo` without replaying its proofs. Reject off-produced, incomplete, stale or incompatible artifacts under the option. In addition to trait openness, make the termination effect of imported operations a specification: a client must never discover a new obligation by inspecting a changed private body.

Replacing the current composition-dependent `OpenTraits` calculation while leaving `ReachesOpenDispatch` at the interface boundary would **not** establish the requested modularity theorem.

## 1. Recommendation and scope

Use the existing `{:termination false}` trait attribute as the declaration of extensibility **when `--modular-termination` is on**. In that mode it does not assume termination. Treat every extensible, overridable slot as open in its producer, before any foreign implementation exists. Reject foreign extension of a sealed trait in the extending module; `@AssumeCrossModuleTermination` cannot override that rule.

Add one optional checked specification attribute, `{:termination_closed}`, for code that cannot reach open dispatch or an unknown callback. This preserves useful closed-code exemptions of M2–M4a across implementation changes. At an import boundary, absence of this promise means `General`, with the declared global bound or TOP if no bound is declared. Do not infer a stronger public promise from a hidden body. Local inference remains available inside a producer.

Store a versioned on-only verification record in `.doo`, bound to the producer's source snapshot, exact semantic build, contracts, options and completed proof obligations. Dependencies are matched by **public contract fingerprints**, not implementation hashes. A library's own body change requires a fresh record for that library; an unchanged public contract leaves dependent records applicable.

Global and callback bounds remain ordinary, duplicable specifications. No permission consumption, linearity or separation logic is introduced. M4b uses the existing tuple order through an abstract comparison interface. #245 may supply other verified orders without changing the extension, call, substitution or artifact rules.

This design does not claim to make all of `Std/Actions` checkable by adding openness markers. Section 9 identifies M4a rank-computation and M3 stored-arrow limitations. A fully certified standard-library distribution is an acceptance gate, not an exemption.

The relevant lesson from Jacobs, Bosnacki and Kuiper is that dynamic-call levels belong in interfaces and implementations satisfy them modularly. The ECOOP 2015 paper develops call permissions; the 2018 treatment presents sequential object levels before its separation-logic and concurrency extensions. This design uses the sequential specification principle and leaves richer order instances to #245. [ECOOP 2015][ecoop]; [TOPLAS 2018][toplas].

## 2. Evidence and two different defects

### 2.1 The library-openness reproducer

The previous revision reports this experiment on the M4a release. Its results are retained as prior evidence; **this revision did not rerun Dafny**.

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

The prior report found that either an on- or off-produced library, consumed with the option on, verified the false assertion. Joint source checking rejected H's newly open call because `n < n` fails. The refreshed resolver gave the same distinction in the reported on-produced case. See [the original revision][old-design] for commands, versions and the precise scope of those experiments.

Under this design, A.T is sealed. B.C is rejected at its `extends A.T`; no obligation in the already checked A is reopened. If the author makes A.T extensible, H must satisfy the strict check **while A is built**. Its current body fails. Increasing H's bound alone cannot admit the cycle: the return call from C.f must then satisfy M2. The failure belongs to the producer that violates its contract.

### 2.2 Declared openness alone is insufficient

The current `CheckModularTermination` returns immediately for a known callee whose body does not reach open dispatch. `Classify(KnownCode)`, rank closure, and type/constant checks also consult body-derived reachability. [Analysis][mt]; [VC generation][decreases].

Consider an already extensible T.f(n) bounded by n, and a library **method** H(t, n) bounded by n + 1. Initially H returns zero. Later its private body returns t.f(n). Both bodies can satisfy the same H contract. A client with bound zero calling H(t, 1) currently acquires a new check only after the second body appears: the library bound 2 must be at most 0. Declared openness alone does not prevent that change.

The corrected rule checks the imported call from its public `General(n + 1)` contract in both versions. That client fails in both; a client with bound at least 2 can succeed in both. Alternatively, H can promise `termination_closed`: its first implementation can check, and changing to the second fails **in H's producer**.

A method avoids another ambiguity: a function definition exposed for unfolding is part of the client's logical specification. Changing a revealed equation is not an unchanged-spec replacement. Section 8 defines the specification without treating private-body reachability as an extra, hidden condition.

### 2.3 Source facts

| Existing location | Relevant fact |
| --- | --- |
| `Resolver/ModularTermination.cs`: `OpenTraits` | Seeds openness from declaration attributes and foreign extensions in the final program. |
| Same file: `IsOpenDispatch` | Uses `Body == null`; export cloning can erase a body, so this is not a stable runtime-slot identity. |
| Same file: `ReachesOpenDispatch`, `Classify`, `ReachingType`, `ReachingChain` | Hidden bodies influence client checks, function-value eligibility and M4a. |
| `AST/Modules/ScopeCloner.cs` | Erases exported method bodies, provided function bodies and hidden constant initializers. |
| `Resolver/ModuleResolver.cs`: parent-trait admission | Permits a foreign sealed parent when the child has `AssumeCrossModuleTermination`. |
| `DooFile.cs`: `ManifestData`, `Validate` | Records assembly file version, currently `4.11.0.0`, rather than an exact fork proof build; no modular proof-completion record. |
| `DafnyFile.cs`: `HandleDooFile` | Unconditionally marks retained library source as already verified. |
| `Backends/Library/LibraryBackend.cs`: `Compile` | Serializes `AfterParsingClone`, which does not establish proof completion. |
| `Commands/BuildCommand.cs` | Modern `build` uses `Legacy/SynchronousCliCompilation`; both it and modern `verify` need completion plumbing. |
| `BoogieOptionBag.HiddenNoVerify` | Permits library production without proofs; its ordinary library-option check is deliberately permissive. |

Paths above are below `Source/DafnyCore`, except driver paths below `Source/DafnyDriver`. Source links are collected at the end.

## 3. Interfaces, identities and openness

### 3.1 Verification units

A producer checks a module realization, or a finite package of realizations, against dependency contracts. Private call-graph/effect inference is allowed locally. Across **every module import boundary**, even when modules are supplied together as source, use the same public termination contract that separate `.doo` compilation would use. Co-loading source must not silently strengthen an interface.

Proofs may use private facts inside their owning realization. Client Boogie contexts contain only the selected export view, its termination contracts and the public facts authorized by visibility. Retaining private source in `.doo` does not grant proof visibility.

### 3.2 Canonical identities

| Entity | Identity and interpretation |
| --- | --- |
| Trait or callable slot | Stable declaring module/contract identity, declaration identity and type arguments; the slot records whether overriding is possible. |
| Alias/export view | Same underlying trait/slot plus view and substitution. Visibility can change; extensibility and slot kind cannot. |
| Abstract import | A named contract parameter, not a view of a secretly selected implementation. |
| Concrete refinement/replacement | A distinct realization checked against its advertised contract and actual bindings. Shared origins do not imply shared proof completion. |
| Synthetic evaluation node | Stable owner plus role: constraint, witness, constructor/formal default, constant initializer or iterator operation. |

Use stable declaration IDs and canonical syntax for durable records. Never serialize AST addresses, object hashes, local paths or transient clone numbers as semantic identities. Alpha-renaming bound variables does not change a contract; changing visible nominal identities does.

Slot classification must survive `provides`, `reveals`, aliases and abstract signatures. A missing visible body does not imply an abstract overridable slot, and a present body does not imply a final method. Audit actual resolver/backend overridability. Conservatively treat every overridable instance slot of an extensible trait as open. A default-body call uses the exact rule only if resolution fixes its target independently of runtime dispatch.

### 3.3 Exact extensibility rules

With the option on:

1. A non-`object` trait is extensible exactly when its defining declaration has `{:termination false}`. Otherwise it is sealed against foreign extension, independently of the implementation set.
2. A foreign class, datatype or trait may extend it only if the parent and every non-`object` ancestor through which subtype membership is obtained are declared extensible.
3. A same-module child may extend a sealed parent, but may not itself be declared extensible while a non-`object` ancestor is sealed. Diagnose the child declaration; do not silently open ancestors by closure.
4. A sealed child of an extensible parent is allowed. Calls through the parent remain open.
5. `@AssumeCrossModuleTermination` is redundant when these rules allow extension and cannot bypass them otherwise. Keep it in migrated sources if needed for off-mode compatibility.
6. Export aliases cannot change openness. Refinement preserving a trait's identity preserves its policy; a distinct new trait can have a different policy. Changing a published policy changes the contract.
7. Built-in `object` retains its special treatment; it supplies no exemption for user traits.

All implementations of a sealed trait belong to its checked realization. A refinement adding local implementations is a new realization, checked once with those additions. It cannot append implementations to an admitted sealed realization while reusing the old proof record.

Reusing `{:termination false}` avoids a second trait marker in this migration. Document its checked on-mode meaning and distinguish it in audit. A future `@Extensible` spelling could be an alias; it is not necessary for M4b.

## 4. Termination effects and call rules

### 4.1 Public effects

| Effect | Public promise |
| --- | --- |
| `Closed` | Execution and specification evaluation cannot reach open dispatch, an unknown function-value application, or a dependency lacking the necessary closed promise. Ordinary termination checks still apply. |
| `General(b)` | Code may use dynamic/callback code and is verified under advertised entry bound b. |
| `General(TOP)` | No proper outer bound is promised; the ordinary unbounded fragment remains available as specified below. |

`{:termination_closed}` declares Closed. Otherwise a callable is General with its explicit/inherited global bound, or TOP. This default is declaration-based, **not body-based**. A global clause on a closed callable is allowed and remains contractual, although exact callers may use its closed exemption. Closed plus callback clauses is rejected initially: such a callback clause admits general unknown code.

Check the closed promise over body, pre/postconditions, frames, ordinary/global measures, defaults, implicit evaluations, constants, by-method bodies and captures. Private closed helpers can be inferred locally. Imported helpers supply closed evidence only through public promises. Closed does not exempt loops or local recursion from existing termination checks.

Do not let any overridable slot of an extensible trait use Closed to evade strict dispatch checking, whether or not it has a default body. It needs a proper global bound when dispatched. Independently stratified closed virtual implementations are a possible future extension. Sealed slots may publish Closed, checked against every implementation of their realization.

Inference may suggest a closed attribute or a bound, but installing it changes the declared interface and must be visible in source/review. Automatically republishing a different inferred effect after every body edit would merely disguise the modularity failure.

### 4.2 Implicit evaluation contracts

M3's constraints, witnesses, component/formal defaults, constant initializers and iterator operations also require stable effects. Record them by owner/role. If the whole computation is public signature syntax, classify it from that syntax and imported contracts. If a hidden provider computation can be invoked, default to `General(TOP)`; an erased body never means Closed. Public signature structure can establish that a hook does not exist, and that structural fact belongs to the interface.

Allow `{:termination_closed}` on constants, type declarations and iterators for their relevant evaluation nodes. On a type it covers constraints, witnesses, field/component type evaluations and defaults, **not virtual members**. On an iterator it covers its constructor/MoveNext protocol and specifications. Formal defaults belong to the callable signature. Validate attribute placement rather than silently ignoring it.

M4b adds no proper-bound syntax for synthetic nodes. A bounded caller cannot invoke a hidden General(TOP) node; it needs a public closed promise or a public closed definition. Retain M3's conservative type-mention over-approximation, but make boundary decisions use only the selected interface. Count these annotations in the census.

### 4.3 Calls and overrides

Let B be the caller's captured entry bound, and b the public callee bound evaluated at the call. Let `<=o` and `<o` be the relations in section 6.

| Call kind | Obligation |
| --- | --- |
| Exact call to locally established or imported Closed code | Existing local/SCC/module termination discipline; no outer comparison. |
| Exact call to General(b) | `b <=o B`, plus the existing ordinary discipline, even if the private body is currently a leaf. |
| Open dispatch | `b <o B`; implementation satisfies the slot contract. |
| Bounded callback application | Certified callback bound c satisfies `c <o B`. |
| Dispatch-to-implementation administrative step | Implementation bound `<=o` slot bound; no second strict decrease. |

A **proper bound** is an element of the order carrier rather than the adjoined TOP; it may be an infinite ORDINAL. An activation with such a bound is called bounded. All proper bounds are below TOP; TOP exact calls can be nonincreasing from TOP. A bounded activation cannot enter General(TOP). Open dispatch without a proper advertised bound fails, including from TOP. TOP is not a source value users can fabricate.

An equal outer bound never excuses arbitrary recursive calls: ordinary decreases remains the secondary discipline. Helper-bound inference remains absent. A helper used by a bounded caller must declare a sufficient bound if its effect is General.

Overrides inherit global/callback contracts when omitted. An explicit implementation bound must be no greater than **every** inherited slot bound after substitution, under the parent's precondition and appropriate entry state. Check effective inherited bounds, not just the immediate parent's syntactically declared clause. Preserve precondition/frame/postcondition compatibility. Callback contracts remain invariant/inherited as in M3. Preserve any Closed promise in permitted override/refinement/replacement cases.

### 4.4 Callbacks and function values

Retain M3's evidence forms:

* A directly supplied lambda is checked under the callback bound fixed when arguments are evaluated.
* Forwarding a bounded input callback proves that its entry bound is at most the receiving formal's allowance. Application separately proves strict decrease from the current activation.
* A named imported function is known closed code only when its **public** effect is Closed and it satisfies the current restriction on callback-bearing functions.
* Immediate lambda application retains its checked lexical-body rule and ordinary termination obligations.

Values stored in fields, datatypes, collections or returned by functions still have no proper-bound certificate. They cannot be applied from a bounded activation or passed as bounded callbacks. A constructor callback clause does not attach evidence to a field. Extensional equality cannot transfer a certificate to a different computation.

Cover `f.requires`, `f.reads`, sequence/array initialization, defaults, nested lambdas and by-method bodies. Retain M3's restrictions on bounded input callbacks inside lambdas and initializer positions until separately extended.

Preserve existing TOP-only unknown-arrow applications. They rely on Dafny's ordinary higher-order termination, formation and naked-function rules. A bounded activation cannot enter such a callback, and a Closed promise cannot contain one. Section 7 states the baseline lemma needed here; do not replace it with the incorrect assertion that every unknown arrow has a static call-graph edge.

### 4.5 Independent bound computation: M4a

Every global/callback bound and order operation used in it must be available independently of the open calls it orders. Retain M4a's prohibition on reaching open dispatch from a bound. Locally use closure analysis; across imports use Closed promises. A hidden rank helper with default General is ineligible even if it currently returns a constant. Its author can declare and verify `termination_closed`.

Include constraints, witnesses and specification evaluations needed to compute bounds. Keep M4a's `requires`, `ensures`, `reads`, `canCall`, fuel and inherited-bound tests. A bound check cannot unfold an unadmitted definition or the open computation whose termination it establishes. This is independent verification stratification, not merely a runtime cycle test.

Preserve entry snapshots and callback capture snapshots. Mutating a rank field does not update the caller's saved bound. Order operations and supporting rank lemmas must themselves belong to the independently admitted closed stratum.

## 5. Artifacts, options, refinement and admission

### 5.1 Chosen compatibility policy

Use strict artifact admission, without an automatic A2 fallback:

| Producer | Consumer | Result |
| --- | --- | --- |
| Off, legacy format | Off | Existing behavior, bytes and diagnostics unchanged. |
| Off or missing modular record | On | Reject with a producer-rebuild diagnostic. |
| On, compatible complete record | On | Validate interfaces and bindings; reuse producer proof without library solver tasks. |
| On, missing/incomplete/stale/mismatched record | On | Reject with the specific mismatch and rebuild instruction. |
| On, new format | Off | Reject the new format with an instruction to enable the option or obtain an off-built library. Do not silently reinterpret marked decreases clauses under another mode. |

The last row is a deliberate restriction for new artifacts, not a change to any legacy artifact's behavior. There is no promise that old Dafny binaries read the new format.

To migrate an off artifact, rebuild its retained source or original source **as a producer**, under declared openness and imported contracts. This may fail in that producer. Do not replay it under a particular consumer's implementation graph and call that modular certification. A future explicit conversion command may automate this producer rebuild; it must not silently fall back to composition-dependent A2 semantics.

### 5.2 Exact stored record

Keep the off writer and legacy format 1.0 model unchanged. On successful checked production, write format 2.0 with an explicitly parsed table. Proposed field names are normative for the first implementation; digest encoding is lowercase SHA-256 hex over UTF-8 or canonical binary data as specified below.

```toml
doo_file_version = "2.0"
# Existing language-version, solver and semantic-option entries remain.

[modular_termination]
schema = 1
enabled = true
status = "complete"
contract_schema = 1
fork_build_id = "<full semantic-build digest>"
source_revision = "<full product revision, descriptive>"
program_sha256 = "<digest of exact uncompressed program entry bytes>"
semantic_options_sha256 = "<digest of canonical effective semantic options>"
proof_profile_sha256 = "<digest of the producer proof profile>"
contracts_sha256 = "<digest of canonical contracts table>"
proof_sha256 = "<digest of complete canonical proof entry>"
coverage_sha256 = "<digest of complete owned-obligation inventory>"
assumptions_sha256 = "<digest of explicit assumption inventory>"

[[modular_termination.exports]]
view_id = "<canonical exported view identity>"
contract_sha256 = "<public verification/link contract digest>"

[[modular_termination.imports]]
slot_id = "<canonical dependency or abstract-import slot>"
view_id = "<required view identity>"
contract_sha256 = "<required public contract digest>"
static_predecessor = true
```

Additional on-only entries are `termination-contracts.json`, `termination-proof.json` and `termination-assumptions.json`, encoded with a versioned canonical serializer. The contracts entry contains the actual interface descriptors, including order IDs, effects, slot openness, nominal/view substitutions, and abstract binding requirements. The proof entry contains the proof profile and obligation inventory; the assumptions entry contains stable declaration/role identities and assumption kinds. Their exact canonical-byte digests are respectively `contracts_sha256`, `proof_sha256` and `assumptions_sha256`. Separately, `proof_profile_sha256` and `coverage_sha256` hash the designated canonical profile and inventory subobjects. Require the manifest import/export lists to match the canonical contract descriptors exactly. Specify sorted keys/arrays where order is semantically irrelevant, integer/string encodings and schema version centrally; never depend on dictionary iteration order.

The proof profile records the exact Dafny/Boogie/prelude identities, resolver mode, semantic plugins, effective verification settings, actual solver identifier/version/binary digest, and any exact-cache provenance used. Record all completed producer-owned parts and imported theorem references. This is a **trusted producer result record**, not a solver proof object. Digests bind records to content; they do not prove an arbitrary forged record true. As with the verifier executable and compiler output, authentic operation of the selected producer is an ordinary premise. M4b does not introduce a signing service or select an alternative for having a smaller trusted base.

No timestamps, machine names, absolute paths, random session IDs or transient AST IDs enter semantic fingerprints. No imported implementation/payload digest is a compatibility condition for a dependent proof. The own `program_sha256` protects this library's implementation; each dependency's `contract_sha256` protects what this library assumed.

### 5.3 Exact fork build and semantic options

`4.11.0.0` and the abbreviated display suffix are insufficient. Generate an embedded full semantic-build digest from a canonical inventory of actual verifier product sources, generated semantic inputs, pinned Boogie package hashes, prelude, semantic build configuration and supported plugins. Include local product modifications, not just a claimed Git revision. A documentation-only commit need not change this identity. Keep the full product revision as a diagnostic aid.

Do not hash generated checked Std certificates into the identity those certificates themselves contain. Fix the semantic build from verifier inputs, build the verifier, produce checked Std artifacts, and then embed them. Treat Std source, overlay recipes and generated library resources as library producer inputs with their own payload/interface/provenance records, not as verifier-semantic-build inputs. Otherwise a body-only Std replacement would invalidate every dependent record despite an unchanged interface. The exact checking-build identity includes the loader/contract logic but excludes bundled library contents; a packaging revision may change those contents without changing the checking-build identity. Unsupported semantic plugins or unidentifiable proof inputs prevent portable complete certification in version 1.

Retain existing `DooFile.Validate` language/option checks. Additionally require the same interpretation profile for retained source: modular mode, resolver engine, language/type-system options, enabled semantic axioms and contract/order schema. Initially require exact effective equality of options that can change parsing, resolution, generated obligations or admitted assumptions; relaxation needs a reviewed compatibility relation. Do not treat a permissive old `NoOpOptionCheck` as proof compatibility.

A consumer may select a different supported solver or resource budget for **its own** obligations. It is reusing an established library theorem, not reproducing a library run. Record producer solver provenance and reject unsupported known-unsound profiles, but do not require the consumer's solver binary to equal it. A verifier/Boogie/prelude/semantic-build mismatch requires rebuilding. #241's exact-result cache has a different solver/seed/limit equality requirement.

### 5.4 Issuance requires complete verification

Introduce a nonserializable producer-completion object, created only after the exact owned obligation inventory succeeds. Bind it to immutable source snapshots, resolved realization IDs, the final contract analysis generation, semantic options and admitted imports. Pass it explicitly to `LibraryBackend.Compile`; `AfterParsingClone`, a command exit code, or an enabled option is not this evidence.

For an on-mode library build, prepare the exact retained `program` payload before proof scheduling and verify its reparsed/resolved producer snapshot under the recorded options. Serialize once, then run the producer proof once on that authoritative representation; do not verify the original and later attach its result to unchecked different serialized text. Preserve a diagnostic source map separately. Reconcile the advertised input interface with the retained interface and fail if serialization loses a required declaration/contract. Compilation/emission uses that same checked snapshot and immutable payload. This adds producer front-end work, not repeated consumer solver work.

Inventory bodies and their well-formedness, ordinary/global/callback termination, frames/contracts, overrides, constraints, witnesses, defaults, constants, lambdas, iterators and by-method parts. Imported certified bodies need not be checked again, but their interface and binding obligations must be admitted. A bodyless abstract contract contributes a parameter obligation, not an imaginary checked body.

Do not issue complete total-correctness records after `--no-verify`, `--hidden-no-verify`, `{:verify false}`, filters, cancelled/failed/unknown/timed-out/out-of-resource parts, omitted body parts or unresolved coverage holes. `ForceCompile` cannot mint a record. A genuinely empty producer has an empty **known** inventory; an unexpectedly empty scheduler is not success. Explicit `decreases *` does not establish total termination and cannot be used in a reusable total-correctness completion. The opt-in complete-composition result must distinguish partial/filtered checking rather than advertise total completion for it.

Concretely, an unfiltered on-mode `verify` or checked `build` returns an error if a required own-source implementation retains a verification/termination bypass or an unbound executable implementation. A filtered command may report success for its explicitly selected checks, labelled partial, but cannot emit complete composition success or a portable record. Apply the same completeness gate to client-owned source and library-owned source. These are opt-in completeness restrictions to include in the census.

An explicitly assumed extern/bodyless contract is a recorded premise outside the owned source-body inventory; its termination/effect promise is conditional, not a checked body. Ordinary explicit `assume`, `{:axiom}`, externally supplied contracts and extern implementation premises remain visible assumptions. Record and propagate them; the theorem is conditional on their validity. They are not a way to waive M4b admission, a missing body proof, an extension rule or a termination obligation. Do not describe an assumption-bearing result as unconditional. A source `assume false` is therefore not a counterexample to the mathematical theorem, and it is not a valid regression companion for this issue.

Concrete refinements must check changed/inherited implementation obligations against actual bindings. Existing inherited-assertion elision, `$_reverifyPost` guards, or reused source origins cannot substitute for a completion covering this realization. Force the relevant checks when they are not justified by an admitted parametric theorem. Avoid duplicating obligations for a pure export-view clone.

### 5.5 Consumer admission lifecycle

1. **Load immutable inputs.** Recognize the on-only schema; validate mode, supported exact build, complete status, program and metadata digests, and source interpretation options. Unknown schemas fail closed. Hold libraries in a pending-admission state rather than immediately exempting them through `AlreadyVerifiedRoots`.
2. **Resolve contracts.** Recompute each exported contract from the selected retained source/signature under its recorded options; match the stored descriptors. Resolve imports by canonical view identity and substitution. Inspect signatures and contract validity, not private bodies to strengthen caller assumptions.
3. **Check bindings.** Match each required dependency contract with an independently admitted or freshly verified producer; check extension permission, override simulation, refinement identities, order compatibility and static dependency obligations.
4. **Finalize admission.** Only then authorize library body-proof exemption. Failed admission is an on-consumer error, never a reason to reuse legacy skip-verification flags.
5. **Verify the consumer.** Use its own complete inventory and only admitted interfaces. The completion records the selected contracts, not a composition-dependent census of hidden implementations.

Reset admission before each semantic resolution generation, including IDE/module-cache paths. Do not clone or serialize live admission objects. Cancellation, AST-changing postpasses, changed bindings or semantic options invalidate pending state. Construct one immutable final analysis after supported postpasses for both resolver engines; avoid a `PostResolve` analysis disagreeing with the program-keyed lazy `Of` cache.

The `.doo` source still needs parsing/resolution in this design. Zero producer **proof replay** is not zero front-end work. #241 can later cache appropriate resolved interfaces.

### 5.6 Abstract imports, exports and refinement

An abstract library theorem is parametric in named import contracts. If it relies on a same-bound or Closed call through import slot X, its public link contract records a static ordering obligation `owner > realization(X)`. At concrete binding, build the graph of these obligations over canonical realizations and require it to be acyclic. A cycle is a binding error in the instantiating client. It cannot be accepted by mutually assuming abstract termination promises.

This graph concerns **declared static dependencies**, not a guessed whole-program dynamic call graph. M1 handles dynamic back edges. In version 1 every direct abstract-import dependency used by an exact call carries the static predecessor requirement. Do not relax Dafny's module legality rules. Reject cyclic concrete bindings even if a richer explicitly ranked abstract-call discipline might prove them terminating; that is a stated completeness limit. Unknown slots must never be treated as lower modules without an ordering obligation.

An uninstantiated abstract template may carry a checked conditional theorem and its remaining requirements, but cannot satisfy a final executable composition's implementation requirement. An actual implementation supplies compatible signatures/effects/bounds and a checked realization. A concrete refinement produces a fresh record for its bodies and bindings. A transparent export view only projects an admitted interface; it neither creates another implementation nor reveals erased private facts.

Opening a sealed trait through an alias, adding an extensible descendant of a sealed ancestor, using a refinement's token identity to reuse the base proof, or treating an abstract import as its `OriginalSignature` implementation are all rejected identity shortcuts.

### 5.7 Other inputs, standard libraries and stale source

For version 1, raw `--library x.dfy` and DLL-retained Dafny source without a matching admitted `.doo` are rejected in complete on-mode with a concrete producer-build instruction. Ordinary source roots and their includes are freshly verified producers; preserve compilation roles independently. A project dependency build can produce its own checked `.doo`; `DoNotVerifyDependencies` cannot bypass admission. A `.dtr` translation record is not a proof record.

Use a separate certified Std artifact set selected only under the option in **both** input pipelines. Preserve legacy embedded `.doo` files byte-for-byte. Common Std and every target variant (`notarget`, `cs`, `java`, `go`, `py`, `js`, plus separately selected arithmetic resources where used) receive normal admission. If a requested on-bundle is unavailable or not completely certified, issue a direct error; never silently load off Std as checked. `--standard-libraries` is opt-in today, so core M4b can be exercised without claiming a complete Std migration.

An unchanged self-contained `.doo` remains a checked **old source snapshot** if unrelated original `.dfy` files are edited later. It cannot know those files should replace it. Project freshness checks should rebuild selected stale outputs; explicit artifact input selects its retained snapshot. A changed `program` entry or changed interpretation options with an old record is rejected. Never mix a proof of old retained source with newly read implementation bytes. Native code/source correspondence retains the normal compiler/linker/extern premise.

## 6. Requirements on bound orders and the #245 interface

### 6.1 Algebraic requirements

For each compatible order family use a carrier W, strict relation `<o` and nonincrease relation `<=o` with:

1. `<o` is well-founded.
2. `<=o` is reflexive and transitive, and contains `<o`.
3. Both absorption laws hold: `a <o b <=o c` implies `a <o c`, and `a <=o b <o c` implies `a <o c`.
4. All bound values are in the validated carrier, with the needed integer lower-bound/typing premises. Evaluation, comparison and any proof operations are total in the closed stratum.

A transitive well-founded strict order with `<=o` defined as equality-or-strict is sufficient. Totality, antisymmetry of a larger preorder, a computable global ceiling, a successor operation and a join operation are **not** required for sound composition. Successors/joins can be useful additional library contracts. Well-foundedness of one relation plus an unrelated at-most comparison is insufficient.

Add a fresh greatest TOP for missing bounds; TOP is not strictly below itself. Compare distinct order IDs only through an explicitly checked common order/embedding that preserves the relations and carrier conditions. An implementation cannot choose an unrelated private order for an inherited bound. Order identity, parameters and embeddings belong to the public specification.

Provide an internal adapter with operations conceptually named `CheckCarrier`, `CheckStrict`, `CheckNonIncrease`, `Substitute`, `Snapshot` and `CanonicalContractId`. The current implementation dispatches to existing tuple/decreases code, including lower-bound checks. Replace M2's appended-component `AtMost` trick by the adapter's nonincrease operation at its callers; a future order must not be forced to simulate tuple syntax. #245 owns user syntax, order declarations, well-foundedness witnesses and library instances.

### 6.2 The tuple argument has a finite-arity premise

Arbitrary-length TOP-padded tuples are not one well-founded order: `(0) > (0,0) > (0,0,0) > ...`. For the current language, any fixed finite composition has finitely many syntactic clauses and a finite maximum tuple arity. Padding to that maximum yields the finite lexicographic construction used by the proof. Type compatibility and existing component lower-bound premises remain necessary.

Thus the current adapter is sound per finite accepted composition; do not claim its proof covers an unrestricted first-class variable-length tuple datatype. #245 must prove well-foundedness for the actual carrier it exposes.

### 6.3 When richer orders help

Unknown clients do not by themselves require multisets. A numeric callback bound k can be a ghost parameter, the callback contract can use k, and the library's bound can use k + 1. For finitely many natural-valued callbacks, `max(k, j) + 1` works. The library checks these relations without seeing callback bodies. Existing M3 tests use phases such as global `(n,1)` and callback `(n,0)`.

A library cannot choose one fixed finite natural bound that dominates all natural bounds future clients might choose. It must expose a bound parameter, restrict clients, or use an order with a suitable larger element. Changing orders does not eliminate this specification requirement. Numeric heights with explicit composition contracts often cover finite immutable wrapper structures.

Richer orders help when composition replaces one component by an arbitrary finite collection of smaller components while preserving an unknown common context. A Dershowitz–Manna multiset extension can justify replacing `{m} + L` by `{l1,...,lk} + L` when all li are below m, independently of k and the common L. Built-in strict-submultiset decrease cannot introduce those new smaller elements. A verified #245 instance could justify this without changing any M4b rule.

This is an expressiveness/convenience distinction, not a claim that tuples containing ORDINAL cannot encode the same argument. An appropriate ordinal encoding can work, with its own proofs and annotation cost. Existing `Std.Termination.TerminationMetric` proves submultiset properties for its encoding; do not advertise it as an already implemented arbitrary DM replacement order. [Existing metric implementation][metric].

No order extension automatically makes a virtual rank function satisfy M4a, or attaches M3 evidence to a stored arrow. These are separate obligations.

## 7. Soundness argument

### 7.1 Scope and boundary invariant

The claim is total termination of each **complete, accepted total-correctness composition**, under ordinary Dafny premises: sound base logic/ordinary termination rules, valid explicitly recorded assumptions, and correct compiler/runtime/extern correspondence. Partial/filtered checking, explicit nontermination permission and uninstantiated abstract implementations do not constitute such a completion. For an assumption-free composition of verified source, the result has no extra cross-module termination assumption.

For every admitted producer, establish the following invariant:

* Its emitted public contract is independent of private bodies except for facts explicitly exposed as public definitions.
* Every owned implementation satisfies that contract under the named imported contracts and static-order obligations.
* Every extensible slot has been treated as open during producer checking, and sealed realizations cannot be extended after admission.
* Every relevant General activation carries its advertised entry bound. Exact General steps are nonincreasing, open dispatch/certified callback steps strictly decrease, and implementation selection/forwarding respects nonincrease.
* Closed computations terminate under the ordinary discipline and cannot return to the General/open chain through an unaccounted call.
* Bounds and comparisons are justified in an independently admitted closed stratum.
* The result record belongs to the exact checked implementation, options and proof inventory, and every imported premise is either discharged by an admitted producer or remains an explicit abstract/ordinary assumption.

### 7.2 Preservation at boundaries

Producer verification establishes the invariant using contracts for dependencies. Export projection preserves it because it changes visibility, not slot identity or promises. An override with bound i under slot bound s satisfies `i <=o s`; a caller establishes `s <o B`; absorption gives `i <o B`. Ordinary dispatch is a finite administrative step.

For callbacks, producer evidence establishes actual callback bound a at most the receiving allowance c. The receiving code proves `c <o B` on application, hence `a <o B`. The same callback may be used repeatedly: call depth uses the order, and ordinary loop/recursion checks establish finiteness of repeated sequential uses. There is no consumed resource.

A later extension cannot change the producer's obligations: either its trait was extensible and already checked abstractly, or extension is rejected in the client. A new concrete refinement must establish its own realization invariant. Abstract bindings discharge the recorded acyclic static order and contract simulation before completion. Artifact validation prevents applying a theorem to changed payload/options or incompatible interfaces.

### 7.3 Excluding infinite executions

Assume an infinite execution in a complete accepted composition. Ordinary loop/local-termination checking rules out an infinite execution with only finitely many relevant nested calls; equivalently, use the usual call/loop expansion and finite-branching argument to obtain an infinite nested activation chain.

An infinite chain cannot enter independently terminating Closed code. Once a General chain enters a proper bound, it cannot reset to TOP. If it contains infinitely many open dispatch/certified callback steps, weak steps do not undo strict progress. Absorption extracts an infinite descending `<o` sequence, contradicting well-foundedness.

If only finitely many such steps occur, the remaining tail is covered by the ordinary termination discipline: statically ordered imports descend a finite acyclic order, local recursive SCCs descend their local measures, and ordinary higher-order calls use Dafny's existing formation/termination argument. This last clause is essential for the preserved TOP-only arrow fragment; **SCC reasoning alone is not a proof about arbitrary unknown function values**.

This is a relative soundness argument for the new composition rules, not a mechanized proof of all of Dafny, Boogie or M1–M4a. Before a release claims to close #211, review the baseline higher-order tail lemma and the implementation coverage of implicit evaluations against this invariant. If a route lacks that justification, reject that route or supply its proof; do not assert that the abstract argument covers missing implementation obligations. No new counterexample to the existing TOP-only fragment was found in this source review.

### 7.4 Why circular logical ranks remain excluded

The proof assumes actual well-founded values before using their decrease. M4a's closed stratum supplies this independence, including specification evaluation and `canCall` paths. A library cannot certify a rank by assuming the equations of the open computation that the rank is supposed to justify. Admitted imported rank helpers supply stable Closed contracts; changed private bodies must re-establish those contracts producer-side. Order well-foundedness witnesses from #245 must obey the same noncircularity requirement.

## 8. Modularity theorem and exact meaning of specification

Let I(L) be the **public verification and link interface** of the view of L selected by a client. It contains:

* Nominal declaration/type/slot identities, signatures, characteristics and available constraints.
* Preconditions, postconditions, frames, declared/inherited/signature-derived ordinary decreases contracts, global/callback contracts and their entry-state meanings. Exclude private module/SCC prefixes from this interface.
* Trait extensibility, canonical overridability/exact-target information, and callable/implicit-evaluation Closed/General effects.
* Order identities, carrier conditions, embeddings and public proof facts used in bound comparisons.
* Export visibility, reveal permissions and every exposed function equation, constant value or representation fact the client may use.
* Named imported/abstract contract slots and static dependency-order requirements used by the producer theorem.
* The semantic interpretation profile and public proof attributes that affect client verification.
* The exported theorem's unchecked assumption footprint and trust requirements. A replacement cannot introduce a new assume/axiom/extern premise while satisfying an earlier unconditional dependency theorem. Assumption origin details may be diagnostic-only, but premise identities and meanings are contractual.

Private method/opaque-function bodies, private call graphs/SCC numbers, producer proof traces and own implementation hashes are **not** part of I. Signature-only inferred contracts can be canonicalized; a fact inferred from a private body must be explicitly adopted into the declared interface before clients can depend on it. Additional hidden imports cannot introduce new public ordering premises while claiming the same I.

**Theorem.** Fix a client C, its selected export views/bindings and a supported semantic build/profile. Suppose L and L' both verify against I, both have valid own result records, and `I(L) = I(L')`. Then every previously admitted dependency premise about L remains discharged by L', the client's language-level obligations are the same, and its existing proof remains valid. Thus every previously accepted complete client remains accepted by the proof system after replacement, without rechecking dependent library bodies.

Proof: interface resolution and artifact dependency matching use I alone. Each imported call, implicit evaluation, rank check, callback check and override obligation is generated from I alone. Extension/binding decisions use the same nominal, openness and ordering facts. Client-visible logical axioms are the same. Therefore the generated client verification problem is equal up to stable renaming, and the old proof discharges it. L' supplies a new proof of the same imported theorem. Induct over the dependency/binding structure.

The theorem intentionally includes revealed equations. If a client can unfold `function H` and its equation changes, its verification specification changed, even if `requires`, `ensures` and bounds did not. Methods and opaque/provided function bodies are the appropriate body-only substitution tests. Independently, the termination **effect** must remain declaration-based even when a body happens to be visible; do not infer extra termination permissions from that visibility.

The theorem is about proof preservation. A wall-clock-limited SMT run can vary with scheduling and solver history. To approach literal operational acceptance preservation, isolate client translation from private declarations, artifact hashes and private numbering, and require normalized client-VC equality for the body-only regression. With the same deterministic solver context/limits, the same query is the strongest practical gate; do not promise identical wall time. #241 must not invalidate a dependent theorem merely because a dependency's private payload changed.

## 9. Completeness and annotation burden

### 9.1 What A2 accepts that this design can reject

This is relative completeness against the available contracts and ordinary verifier, not decidability of semantic termination.

| Program accepted by final-composition replay in a suitable world | Cost under the chosen modular rules |
| --- | --- |
| A harmless foreign implementation of an originally sealed trait, admitted by the child attribute | Rejected until the trait author declares extensibility. The client cannot unilaterally broaden the contract. |
| A declared extensible trait used only with a favorable finite implementation set | Producer must handle all conforming implementations. Trait and forwarding helpers can need bounds even if this particular composition terminates without them. |
| A bounded client calls a hidden leaf whose advertised bound is too large or absent | Rejected from General(bound/TOP); author supplies an adequate bound or a verified Closed promise. |
| A private-body leaf is used as a rank helper or a named callback | Requires a public Closed contract; hidden implementation analysis cannot justify the client. |
| An erased constraint/default/initializer happens to be harmless | Needs a stable closed evaluation contract if a bounded caller can evaluate it. |
| A source-compatible off-produced or differently built library | Must be rebuilt once by a compatible on-producer. |
| A terminating concrete abstract-import binding with a static cycle | Rejected by version 1's static predecessor policy. |

Some programs rejected by current M1–M4a remain rejected under **both** alternatives: helpers with no usable proper bound, ranks computed through open dispatch, arbitrary stored/returned callback values in bounded code, and bounded use of types/constants/iterators without suitable evidence. M4b must not count those as newly introduced losses without the base-on comparison.

The best annotation tradeoff here is explicit Closed promises for reusable closed APIs and explicit global/callback levels for open APIs. Defaulting all imports to General is conservative but permits libraries to evolve from a leaf to open-reaching code within their existing bounds. Defaulting them all to Closed would move the opposite restriction to ordinary library evolution and would be unsuitable for existing callback libraries. Inference can later suggest stable declarations, but cannot erase the interface distinction.

### 9.2 Measured static inventory: the 20 Std markers

These counts were obtained from the pinned source, **not from running a candidate verifier**. All 20 occurrences in Std are in `Std/Actions`; 8 decorate traits and 12 decorate classes.

| File under `Source/DafnyStandardLibraries/src/Std/Actions` | Sites | Declarations |
| --- | ---: | --- |
| `Actions.dfy` | 3 | Action, TotalActionProof, DefaultTotalActionProof |
| `Consumers.dfy` | 7 | IConsumer, Consumer, ArrayWriter, DynamicArrayWriter, FoldingConsumerTotalActionProof, SeqWriter, SeqWriterTotalActionProof |
| `Producers.dfy` | 4 | IProducer, FunctionalIProducer, Producer, OutputterOfNewProducers |
| `BulkActions.dfy` | 6 | BulkAction, BatchReader, BatchSeqWriter, BatchSeqWriterTotalProof, BatchArrayWriter, BatchArrayWriterTotalProof |

DefaultTotalActionProof, ArrayWriter and SeqWriter extend only same-module parents, so **17 sites** actually authorize a foreign edge. The foreign-parent set, including its non-object ancestor closure, is exactly **seven traits**:

`Std.GenericActions.GenericAction`, `Std.Frames.Validatable`, `Std.Actions.Action`, `Std.Actions.TotalActionProof`, `Std.Producers.Producer`, `Std.Consumers.IConsumer`, and `Std.Consumers.Consumer`.

Seven declaration-side openness markers therefore preserve the **current in-corpus inheritance permissions**. Keep the old child attributes as needed for off compatibility. There are 13 public trait declarations under Actions, plus Validatable. Keeping all these extension/proof protocols available to unknown clients suggests **14** declarations, also including BulkAction, IProducer, ProducesSetProof, ProducerOfSetProof, ProducerOfNewProducers, OutputterOfNewProducers and ActionCompositionProof. This is a recommended API policy; seven is only the measured minimum for today's edges.

The seven mandatory-open traits contain 26 textual bodyless declarations, or **21 distinct introduced slots** after inherited overrides are counted once:

| Introducing trait | New slots | Count |
| --- | --- | ---: |
| GenericAction | Requires, Reads, Modifies, Decreases, Ensures, Invoke | 6 |
| Validatable | Valid, ValidChange, ValidImpliesValidChange | 3 |
| Action | ValidHistory, ValidInput | 2 |
| TotalActionProof | Action, AnyInputIsValid | 2 |
| Producer | ProducedCount, Remaining, ValidOutputs, DecreasesMetric, ForEach, Fill | 6 |
| Consumer | DecreasesMetric, Capacity | 2 |
| IConsumer | No new bodyless slot; Accept has a body | 0 |

This is a minimum **contract-review inventory**, not a verified total of required global clauses. The canonical-slot audit can add overridable bodyful methods, and concrete helpers and specification calls also need contracts. The pinned Std has **zero global or callback decreases clauses**. The earlier issue comment projected 185 helpers after adding dispatch bounds; that was an earlier classifier projection, not a measured migration count under this design. [Prior census/status][status-m3]; [Std Actions sources][actions].

### 9.3 M4a prevents mechanically promoting existing decreases

`GenericAction.Invoke` currently uses ordinary `decreases Decreases(i), 0`. Decreases(i) is itself a virtual ghost function. Making it a global bound would violate M4a after GenericAction becomes open. Producer/consumer metrics also pass through `DecreasesMetric().Ordinal()`. A richer bound carrier does not make that rank computation independent.

Attainable first techniques are directly readable ghost rank fields/constants, constructor contracts relating parent and child ranks, closed computations over rank data, and separately designed global phases. Preserve local decreases for local recursion/loops rather than copying every local measure into a global clause. For example, ordinary `(Repr,0)` and `(Repr)` are ordered differently once the implicit local/module prefix is removed: `(Repr)` has the larger trailing TOP. Phases need deliberate design.

Finite immutable composites often admit natural height plus a phase. Do not claim this covers all Actions: mutable component replacement and dynamically produced producers need explicit invariants/contracts. `Frames.dfy` already discusses why Repr is not a universal decreasing measure when components can be allocated. [GenericAction][generic-action]; [Frames][frames].

### 9.4 M3 prevents a marker-only stored-callback migration

The source has five explicitly arrow-typed fields in the relevant families:

| Field | Role |
| --- | --- |
| FunctionAction.f | Runtime and specification applications |
| TotalFunctionActionProof.f | Ghost stored arrow |
| FunctionalIProducer.stepFn | Runtime application |
| FilteredProducer.filter | Runtime and specification applications |
| FoldingConsumer.f | Runtime and Seq.FoldLeft reasoning |

The four runtime families cannot receive persistent evidence merely by adding a constructor callback clause: M3 classifies later field selection as unknown/TOP. Full migration needs a separately reviewed persistent bounded-callable design or an explicit bounded-object representation/API change. Wrapping an arbitrary unknown arrow in an object does not manufacture the missing evidence. #245's richer orders solve neither this problem nor virtual rank acquisition.

Until those obligations are resolved, do not advertise a fully checked Std bundle or issue a fake complete record. A core M4b implementation can reject unsupported Std use with a clear diagnostic while separately checked modules migrate. The eventual full Std milestone is blocked on this work, not silently waived. Supporting APIs such as Seq also need appropriate Closed or global/callback contracts; count this transitive work rather than only the 21 slots.

### 9.5 Keep the off library unchanged

Maintain a versioned, reviewed on-only Std source overlay/patch set and build recipe. It adds the new contracts/rank data and any agreed API adaptations to a temporary source tree used only for certified artifacts. Record the exact resulting source in the artifact and include overlay/recipe inputs in that library's producer provenance inventory. Do not automatically infer and inject unpublished contracts during consumer loading.

Leave the existing Std source/artifact production path and legacy embedded resources unchanged. Merely adding `{:termination false}` to common Std sources could alter off-mode assumption/termination behavior; placing additional global clauses there could also alter off parsing/serialization. The overlay prevents those changes. It adds maintenance cost against upstream; a later deliberate unification requires its own byte-identity evidence.

### 9.6 Census protocol and reporting

Run four configurations on identical unmodified corpus snapshots: base-off, candidate-off, base-on and candidate-on. Then separately run an openness-only migration (seven mandatory declarations) and the progressively migrated contracts. Keep legacy and refreshed resolver runs separate; do not count fallback-to-refresh as testing both engines.

The existing census is **resolution-only**, useful for sites and projected helpers, not verification verdicts. Start with its current commands:

```sh
python3 .github/review/modular-termination-census.py "$dafny_211" lit \
  Source/IntegrationTests/TestFiles/LitTests/LitTest census/lit
python3 .github/review/modular-termination-census.py "$dafny_211" project \
  Source/DafnyStandardLibraries/src/Std/dfyconfig.toml census/std
```

Extend it to record declared-open traits, rejected extension edges, canonical open slots, public Closed contracts, distinct introduced/inherited bounds, callback clauses, rank fields, constructor constraints, new preconditions/signature changes, supporting lemmas and stored-arrow/API adaptations. Record error categories separately: extension permission, missing bound, strict/nonincrease comparison, closed-effect violation, M4a closure, unknown callback, TOP implicit evaluation and artifact admission.

Add a verification driver over the actual Lit RUN configurations and Std project targets. Example direct project probes after building the candidate are:

```sh
"$dafny_211" verify Source/DafnyStandardLibraries/src/Std/dfyconfig.toml \
  --standard-libraries:false --modular-termination --cores:1 \
  --verification-time-limit:60 --solver-path:"$z3_211"
"$dafny_211" verify Source/DafnyStandardLibraries/src/Std/dfyconfig.toml \
  --standard-libraries:false --modular-termination --type-system-refresh \
  --cores:1 --verification-time-limit:60 --solver-path:"$z3_211"
```

These are commands to run, not claimed results. Supply exact binary/build IDs, solver/profile, corpus commit, target, resolver, options, per-declaration verdict/RU, wall time, proof-task counts and peak memory with each run. Separate genuine newly required annotations from parser/unsupported-feature failures, base failures, timeouts and resource exhaustion. Include separate library-build/client-load experiments; monolithic source census alone does not test modular replacement.

Publish actual counts and patches. No candidate rejection count or complete Std annotation count is claimed in this design. The earlier M4a zero-rejection result is not evidence for M4b.

## 10. Alternatives, with modularity required

| Alternative | Soundness condition | Modularity | Relative completeness and annotations | Format and cost |
| --- | --- | --- | --- | --- |
| A1: replay only newly openness-dependent obligations | Exact logical dependency closure, including WF, canCall, rank/spec and inherited effects | Fails if openness/effects depend on the consumer; exact selective replay does not fix that | Can exploit a favorable final implementation set; hard to identify all proof dependencies | May avoid some solver work; still repeats context-dependent checks |
| A2: full consumer replay | Complete verification of actual final composition | **Fails the required substitution property** | Accepts more closed/specialized compositions without universal contracts, but private body changes can break clients | No new format; redoes library proofs in every client |
| B: manifest facts only, without a changed producer rule | A recorded fact must be proved and applicable to every allowed extension | Fails if facts encode final-program openness or hidden reach; an enabled bit is insufficient | Superficially low annotation burden, but not an adequate solution | A schema alone cannot repair the theorem |
| C1: seal all library traits | Enforce sealing through identities, subtraits and refinement | Possible, with stable helper/evaluation effects too | Rejects all useful foreign extension; fewer open bounds | Simple loading but severe API restriction |
| C2: demand a bound only at foreign extension | Must also prove producer callers and helpers against it | Insufficient alone; the reproducer already has bounds | Low apparent cost conceals missing producer obligations | Cannot justify reuse by itself |
| D: A2 plus exact-result caching | Fresh final admission plus a correct result key | Still fails if obligation generation depends on hidden implementations | Same logical acceptance as A2 | May reduce repetitions, not restore contractual substitution |
| E: universally open all exported traits plus stable effects and records | Check all producer code against arbitrary conforming overrides | Yes | More bounds on deliberately closed APIs; loses sealed-world proofs unnecessarily | Same record protocol, potentially more producer work/annotations |
| **Chosen: declared openness + stable effects + checked records** | Sections 3–8 | **Yes**, for the precise public interface | Sealed reasoning retained; open APIs need contracts; explicit costs in section 9 | New on-only format; producer proofs reused, signatures still resolved |

An automatic replay fallback using **the same declared contracts and producer boundary** is modular, but is producer rebuilding, not A2. It can be added for convenience later. Strict rejection first makes mode/build/coverage failure unambiguous. An explicitly separate whole-program A2 tool could accept additional programs; its guarantee must not be confused with modular-library certification.

For a library proof cost P, client proof cost C, and admission/front-end cost A per consumer, n consumers cost approximately `P + n(C + A)` under reusable records, versus `n(P + C + A)` for full replay, ignoring caching and producer edits. A remains material because `.doo` retains source and current resolution is repeated. These are accounting identities, not measured speedups. A changed library body incurs its own new P; unchanged dependent interfaces do not incur dependent proof replay.

The recommendation follows soundness, modularity, completeness and annotation burden. A small trusted base is not a selection criterion. The producer trust premise is stated because it is necessary to interpret the record, not because it favors an alternative.

## 11. Implementation plan and code locations

The following are required changes against the pinned tree, not changes implemented by this PR. Proposed new filenames are explicitly marked. Keep all new semantic paths under `--modular-termination`.

### 11.1 Language, identity and verification

| Location | Required modification |
| --- | --- |
| `Source/DafnyCore/Resolver/ModuleResolver.cs`: parent-trait admission and inherited members | Replace the child's assumption bypass on-mode; enforce declared ancestor openness using canonical identities. Classify actual overridable slots/default bodies and preserve inherited slot correspondences. Emit the sealed-parent diagnostic at the extending declaration. |
| `Source/DafnyCore/AST/TypeDeclarations/TraitDecl.cs`; `AST/TypeDeclarations/TopLevelDeclWithMembers.cs`; member/slot declarations | Store resolved extensibility, effect and stable slot provenance only for the new analysis. Keep generic source attributes for round-trip serialization. Validate `termination_closed` as a nullary attribute on supported declarations; reject duplicate/conflicting/misplaced uses on-mode. |
| `Source/DafnyCore/AST/Modules/ScopeCloner.cs`, `AbstractSignatureCloner.cs`, `ModuleSignature.cs`; `AST/Cloner.cs` | Preserve canonical contract/view identity and substitutions while erasing bodies normally. Distinguish an abstract contract from a transparent view and a new concrete realization. Do not reuse generated `LiteralModuleDecl` GUIDs as durable identities. |
| `Source/DafnyCore/Rewriters/RefinementTransformer.cs` | Carry contracts through refinement, enforce openness/effect/override compatibility, and mark inherited obligations whose realization/bindings require checking. Do not copy live admission/completion. |
| **New:** `Source/DafnyCore/Resolver/TerminationContract.cs` | Typed representation of Closed/General, order descriptors, implicit-evaluation roles, canonical slot/view IDs, abstract requirements and public assumption footprint. Keep producer implementation descriptors separate. |
| **New:** `Source/DafnyCore/Resolver/TerminationContractBuilder.cs` | Derive boundary contracts only from declarations/public definitions; prove declared Closed via local effect analysis. Build versioned canonical descriptors and export projections. Prevent hidden-body inference from affecting caller contracts. |
| `Source/DafnyCore/Resolver/ModularTermination.cs` | Change OpenTraits to declaration lookup; remove foreign-implementation seeding. Replace Body-null dispatch classification with canonical slot kind. Make imported `ReachesOpenDispatch`, `Classify`, `ReachingType`, `ReachingChain` queries contract-based. Retain private inference only within the owning realization; enforce Closed over every evaluation edge. |
| `Source/DafnyCore/Resolver/ProgramResolver.cs`; `Rewriters/RewriterCollection.cs`; `AST/Program.cs` | Build one finalized, generation-bound contract/admission analysis after semantic postpasses in both resolvers. Retain cardinality validation and other independent soundness passes. Invalidate the existing program-only lazy analysis cache when resolution changes. |
| **New:** `Source/DafnyCore/Resolver/ModularTerminationAdmission.cs` | Manage pending/valid/failed admission, abstract binding simulation and static-predecessor DAG checks. Unknown/unmapped identities are errors, not closed effects. |
| `Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs`: `CheckModularTermination`, `AtMost`, callback checks | Emit M2 for every imported General contract; use Closed only from established evidence. Preserve entry/capture snapshots. Introduce the order adapter for strict/nonincrease/carrier checks; do not hard-code DM. Preserve supported M3 argument/application rules. |
| `Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs`: `AddOverrideTerminationChk` | Check all effective inherited contracts and substitutions, including grandparent bounds, multiple parents and bodyful overridable methods. Check callback/effect compatibility and carrier/order identity. |
| `BoogieGenerator.Functions.Wellformedness.cs`, `BoogieGenerator.Methods.cs`, `BoogieGenerator.Iterators.cs`, `BoogieGenerator.Obligations.cs`, `BoogieGenerator.ExpressionTranslator.cs` under `Source/DafnyCore/Verifier` | Audit explicit and implicit call/check producers: specifications, type constraints, defaults, lambdas, .requires/.reads, initializers, by-method and inherited postcondition paths. Each necessary obligation must be inventoried and asserted, not inherited as an unearned assumption. Preserve both consistent-obligation settings. |
| `Source/DafnyCore/Verifier/BoogieGenerator.cs`: scope and assertion handling | Translate consumer proofs from stable admitted interfaces; gate translation on current admission. Prevent private library equations/numbering from leaking into client VCs. Force needed refinement checks instead of relying on inherited assertion elision. |

The new effect is not a free SMT axiom. Closed promises are checked by the producer and imported only with admitted theorem evidence. Review any additional logical assertion separately under the fork's axiom policy; do not quietly add one to make a bound prove. The planned boundary change mainly restores proof obligations and controls reuse.

### 11.2 Producer, manifest and loader

| Location | Required modification |
| --- | --- |
| `Source/DafnyCore/DooFile.cs`: `ManifestData`, `Read`, `Validate`, both writers | Keep legacy model/writer exactly on the off branch; implement explicit 2.0/table schema dispatch, canonical entry digests and contract/profile checks on-mode. Unknown fields/versions have deliberate tests. Do not globally serialize a new false option. |
| `Source/DafnyCore/Backends/Library/LibraryBackend.cs` | Prepare immutable retained payload before proof, accept only a matching completion object, write the on-only entries after success, and publish atomically. Failed verification cannot leave a newly advertised complete artifact; an older valid output is not silently labelled current. |
| `Source/DafnyCore/AST/Grammar/ProgramParser.cs`; `AST/CompilationData.cs`; `AST/ShouldCompileOrVerify.cs` | Track producer ownership, source snapshots and pending/admitted imports. Separate verification roles from compilation roles. A library skip is authorized by completed admission, not input extension. Invalidate `UrisToVerify` when roles change. |
| `Source/DafnyCore/DafnyFile.cs`: `.doo`, source-library and DLL handlers | Require metadata before granting proof exemptions; prohibit raw library/DLL bypasses in complete on-mode. Preserve ordinary source/include checking and off loading behavior. |
| **New:** `Source/DafnyCore/Verifier/ModularTerminationCompletion.cs` | Own exact proof-part inventory and completion aggregation, source/profile/analysis association, assumption footprint and partial-result state. Cannot be reconstructed from command-line flags, cloning or arbitrary cached success. |
| `Source/DafnyDriver/CliCompilation.cs`; `Source/DafnyCore/Pipeline/Compilation.cs`; `Pipeline/TextDocumentLoader.cs` | Expose all required own proof parts, admit dependencies before scheduling client proofs, aggregate failures/partial scopes, and report complete versus partial consistently. |
| `Source/DafnyDriver/Commands/BuildCommand.cs`; `Legacy/SynchronousCliCompilation.cs` | Integrate the actual modern-build path; prevent ForceCompile/no-verify from producing complete records. Verify the retained producer snapshot once and pass its completion to the library backend. |
| `Source/DafnyDriver/DafnyNewCli.cs`: project dependencies | Require checked dependency outputs; block DoNotVerifyDependencies/raw-source exemptions in complete on-mode. Preserve the dependency producer boundary. |
| `Source/Directory.Build.props`; release/build scripts; **new build identity generator** | Embed full actual-input semantic build ID; avoid generated-Std self-reference; preserve existing display/version behavior off-mode. Validate IDs in packaging. |
| `Source/DafnyLanguageServer/Language/CachingResolver.cs`, `DafnyProgramVerifier.cs` | Never clone stale admission/completion from a cached module. Include contract/profile/binding identity in relevant cache ownership. IDE partial checking does not publish complete `.doo` certification. |

Both invocation pipelines must exercise the same admission and completion policy. A fix only in modern `verify`, with library `build` still able to serialize an unchecked parse clone, does not close the boundary.

### 11.3 Std, audit, documentation and #241

Add the reviewed on-only Std source overlay under proposed `Source/DafnyStandardLibraries/modular-termination/`, with a README, pinned patch applicability and explicit build recipe. Update the common/target-specific Makefiles, `Source/DafnyPipeline/DafnyPipeline.csproj` resources and `Source/DafnyCore/DafnyMain.cs` resource catalog for a distinct checked artifact set. Update selection in both `Compilation.DetermineRootFiles` and `SynchronousCliCompilation`. Certification must verify all required parts; the current hidden-no-verify production route cannot be reused for it.

In `Source/DafnyCore/Auditor/Assumption.cs`, `AuditReport.cs`, `TraitDecl.Assumptions` and `TopLevelDeclWithMembers.Assumptions`, distinguish checked extensibility from the old unchecked termination assumptions. An on-mode audit of source alone reports the declared rule, not a proof that has not run. Aggregate admitted-library assumption inventories even when included/preverified members are otherwise omitted by the report. Do not hide unrelated assumptions. Off-mode audit output remains unchanged.

Update `docs/DafnyRef/Specifications.md` section 7.1.3.1, attribute/trait/export/refinement documentation where relevant, `.doo` documentation and `CommonOptionBag.ModularTermination` help. The manual must state all of the following normative changes:

* Openness belongs to the trait declaration; a child cannot open a sealed foreign ancestor.
* General imported calls always use their declared bounds; Closed is an explicit verified promise.
* Dynamic-slot classification survives export erasure/default bodies.
* On-produced compatible artifacts are checked once; incompatible/off artifacts require rebuilding.
* Complete on-mode checking rejects residual unchecked termination/verification and distinguishes filtered partial results.
* Current M3/M4a limitations and unavailable checked Std components remain visible.

Remove the present library-openness warning only when implementation/admission gates pass. Keep the old warning during partial milestones; a documentation-only design PR does not fix the released verifier. Add an implementation release fragment describing the opt-in compatibility changes, not a claim of an already shipped fix.

#241's persistent exact-result cache and M4b's library theorem records remain separate. Exact task-cache lookup occurs after current producer admission and required inventory construction. Its key must cover actual task context, solver/options/seed/limits as that study requires; it can discharge producer parts only with matching coverage. A result hit cannot bypass contract checking, M4a, binding, cardinality admission or completeness. Conversely, stable module theorem reuse must not key dependency implementations into the public contract. Skipping library proof work intentionally changes on-mode work/RU; do not confuse this with #241's same-task equivalence requirement.

## 12. Regression and measurement plan

All tests below are required implementation evidence, not executed results in this design PR. Add a driver such as `git-issues/github-issue-211-m4b.dfy` and small `Inputs/` sources/artifact-building commands alongside existing #211 tests. Add focused unit tests for schema, canonicalization and admission lifecycle in `DafnyCore.Test`, including `DooFileTest.cs`. Test meaningful boundaries rather than duplicating internal implementation expressions.

For **every rejection family**, include a reachable `assert false` companion and check the intended diagnostic/category. An unrelated parse error, timeout or a later false-assertion failure is not evidence that the intended admission check happened. For every accepted execution/proof family, add a valid-input companion where `assert false` must fail, without `assume`, contradictory preconditions or unreachable branches. Instrument proof-task ownership to establish zero library replay, not just elapsed time.

### 12.1 Declaration and client-substitution cases

| ID | Case and expected result |
| --- | --- |
| L01 | Original section 2.1 library + later C: library may build sealed; client fails at foreign extension. No library proof tasks. |
| L02 | Same producer declares T extensible: equal-rank H dispatch fails in producer, before any client exists. |
| L03 | Counter-decreasing cross-module implementation and sufficient helper bounds: accepts; constant-counter variant fails the bound. |
| L04 | Foreign leaf implementation of declared-open trait: accepts with an adequate proper-bound contract; sealed equivalent fails, independent of child marker. |
| L05 | Extensible same-module descendant of sealed ancestor: rejects directly and transitively. Foreign subclass through an alias cannot bypass it. |
| L06 | Sealed descendant of open ancestor: accepts; ancestor-view dispatch remains strict. |
| L07 | Method H changes from closed-looking body to open-reaching body but keeps General bound: client VCs/classification identical. Sufficient-bound client accepts both; insufficient-bound client fails both. |
| L08 | Explicit Closed H changes among valid closed implementations: clients and dependent artifacts remain accepted; changing it to dispatch fails producer-side. |
| L09 | Opaque/provided function body replacement: unchanged client contract and normalized VCs. Revealed-equation replacement changes the contract and invalidates dependents explicitly. |
| L10 | Named function value and rank helper replacement: public Closed eligibility remains unchanged; no hidden-body rediscovery. |
| L11 | Multiple inheritance and grandparent inherited bounds: implementation satisfies every effective contract; wrong variance fails. |
| L12 | Bodyful overridable trait method and provided/revealed views: identical canonical dispatch classification. Fixed-target method/body erasure does not create false dispatch. |

### 12.2 Evaluation, callbacks, order and binding cases

| ID | Case and expected result |
| --- | --- |
| E01 | Hidden constant/constraint/witness/default changes under unchanged Closed contract: producer checks it; client classification stable. General(TOP) variant fails from bounded caller. |
| E02 | Type aliases, nested generic arguments, datatype defaults and iterator MoveNext: no implicit-evaluation route loses its contract. |
| E03 | Immediate bounded lambda and legal bounded callback forwarding: accepts; reversed allowance inequality fails. |
| E04 | Constructor receives bounded arrow and stores it: later bounded field use remains rejected. Returned/datatype/collection variants likewise do not acquire evidence. |
| E05 | `.requires`, `.reads`, sequence/array initializer and nested-lambda restrictions: same rule, with intended diagnostics. |
| E06 | TOP-only ordinary unknown-arrow positive remains accepted; inserting a bounded-to-TOP helper route is rejected. Cover escape/capture paths in the baseline higher-order proof audit. |
| E07 | M4a original exploit, rank through private helper, and spec canCall/fuel variants: reject without vacuity; imported Closed rank helper accepts only after its own proof. |
| E08 | Mutable rank and callback fields/arguments: use saved entry/capture bounds, not a larger post-mutation caller value. |
| E09 | Equal, smaller and greater bounds; missing TOP; incompatible component types; lower-bound premises; differing fixed arities: expected comparisons preserved. |
| E10 | #245 integration contract: unrelated order IDs rejected; a future proved embedding/DM instance works through the adapter with no M4b admission changes. Until #245 exists, unit-test adapter separation without pretending user syntax exists. |
| E11 | Abstract import plus legal realization: checks conditional producer once and binding separately. Unbound executable implementation and cyclic static bindings fail in client. |
| E12 | Concrete refinement changes inherited code/bindings: its necessary inherited obligations run; a facade does not create duplicate body proofs. |
| E13 | A local implementation newly added to a sealed realization must satisfy its public slot effects; no mutation of an old admitted realization. |
| E14 | Cross-module source supplied in one invocation versus separately built artifacts: same interface-level decisions and no private reach inference. |

### 12.3 Artifact and assurance cases

| ID | Case and expected result |
| --- | --- |
| A01 | Entire on/off producer/consumer matrix in section 5.1, for direct `.doo` inputs and `--library`. |
| A02 | Missing/unknown/malformed schema, missing entry, duplicate IDs, unknown identity, mismatched manifest/descriptors: explicit admission failure. |
| A03 | Change program, semantic options, effect, openness, bound, order, export view or revealed definition without matching record: rejects. |
| A04 | Replace dependency body, reverify only that dependency, preserve interface: preexisting dependent `.doo` admits with **zero dependent/library proof replay**. |
| A05 | Change public contract/assumption footprint: reject dependency mismatch. A new unchecked premise cannot satisfy an old unconditional contract. |
| A06 | Wrong exact fork build, Boogie/prelude profile or resolver mode: rebuild diagnostic. Different supported consumer solver for its own checks: accepts compatible library theorem. |
| A07 | no-verify, hidden-no-verify, filters, verify-false, decreases-star, cancellation, failure, unknown, timeout/OOR, missing inherited part: no complete artifact or unfiltered complete-source success. |
| A08 | Known empty module versus accidentally unscheduled nonempty inventory: only the first can complete. |
| A09 | Stale original source beside unchanged selected artifact: old snapshot remains identified; mixing old record with new payload/binary selection is rejected. Project dependency freshness rebuilds when appropriate. |
| A10 | Raw source `--library`, DLL retained source, project skip-dependencies and `.dtr`: no proof-admission bypass. |
| A11 | Cached resolution/verification result from old generation, contracts or scope: cannot supply new completion; valid exact producer cache parts may. |
| A12 | Retained-source round trip: exact checked payload is emitted; omitted clause/default/body or changed interpretation fails producer reconciliation/verification. |
| A13 | Audit propagates dependency assumptions, distinguishes declared versus established checking, and never reports a syntactic marker as a completed proof. |
| A14 | Every available certified Std resource and target variant passes normal admission; unavailable/uncertified bundle errors explicitly. Old resources are never accepted as certified. |

Run relevant source cases under both resolver engines and both consistent-obligation settings; add the fork's axiom option combinations for the existing canCall/override exploit families. Exercise both actual `verify` and `build` paths. Do not multiply every schema unit test by every solver switch: use the combinations that reach different implementation paths.

### 12.4 Option-off byte identity and validation costs

Compare base and candidate with the option off on the existing LitTests and standard-library corpus: verdicts, diagnostics, generated Boogie bytes and deterministic resource counts. Retain exact old option registration, legacy serializer/writer, resource selection and audit behavior. The on-only contract field must not serialize `false`, null or an empty table in old files.

For `.doo`, compare exact `manifest.toml` and `program` bytes, entry list/order, compression and all ZIP metadata, then compare raw archive bytes. The current writer uses time-dependent ZIP/filesystem metadata, so run base/candidate under identical controlled clocks/input timestamps and environment. Do not normalize differences away and call that byte identity. Repeat with unchanged inputs through normal and synchronous build paths. New on-only deterministic writing is allowed; it must not alter the old writer.

Measure producer parse/serialization/resolve/proof/packaging separately; measure consumer load/hash/interface resolution/admission/client proof separately. Record library-owned proof-task counts, interface/hash sizes and peak memory. Reuse is established by ownership counts plus interfaces, not a warm wall-time improvement. Hashing and ordinary graph analysis should be linear in their serialized input/edge sizes; quantify any repeated per-view canonicalization before optimizing it.

## 13. One-PR milestones

| Milestone | One focused PR | Acceptance evidence |
| --- | --- | --- |
| M4b-0 | This revised design, replacing the A2 recommendation | Source/requirement review, consistent theorem/protocol, explicit counts and unexecuted gates. No product change or released-soundness claim. |
| M4b-1 | Declared openness, canonical slots/views, stable General/Closed contracts and source call rules | L01–L12 and source E cases in both resolvers; private-body client-VC equality; declared ancestor diagnostics; initial on-census; off byte identity. Keep imported-library gap warning until admission ships. |
| M4b-2 | Producer payload/coverage completion, semantic build identity, on-only schema and strict loader admission | A01–A13, actual build/verify pipelines, raw `.doo` off identity, dependency body-only reuse without replay. Include refinement/abstract binding completeness before enabling records. |
| M4b-3 | Audit, user documentation, source/project/DLL route integration and cache-lifecycle hardening | No source-bearing admission bypass; partial/assumed results accurately described; #241 boundary; all regression categories intended rather than incidental errors. |
| M4b-4 | Reviewed Std overlay and certified bundle packaging | Seven/14 policy decided and counted, real bounds/effect migration, all targets available only after actual verification; legacy resources byte-identical. Block full Actions certification until stored callable/rank obligations are solved in separately reviewed work. |
| M4b-5 | Composition-aware census, release acceptance and final manual status | Publish four-way base/candidate results, migration patches/counts, source and artifact tests, ordinary higher-order tail/implicit-evaluation proof review and no unaccounted proof gaps. Remove the shipped library-openness warning only at this gate. |

M4b-2 may conservatively reject unsupported dependency forms until M4b-3 admits them; it must not leave an unsound skip path enabled. Core modular libraries can ship with unsupported Std features explicitly rejected, but that is a documented partial rollout, not completion of the full Std milestone. #245 supplies orders independently; bounded stored-function evidence is separate from both.

Each product PR follows `AGENTS.md`/`REVIEW.md`: release fragment, regression driver/expectations, appropriate manual/help updates, updated product/regression/obligation-producer records and the normal CI/verdict gates. Intentional verdict changes receive individual reasons; do not replace expected results wholesale. This design-only PR needs none of the product release bookkeeping.

## 14. Limitations, proof gates and validation of this revision

**Not proved by this document:** a mechanized soundness theorem for existing Dafny/Boogie; full coverage of every implementation path; the complete baseline higher-order tail lemma; or all serializer/canonical-interface invariants in code. The relative argument specifies those obligations. Complete portable certification must not be enabled until the implementation audit and adversarial tests discharge its applicable gates. A source review finding no exploit is not a proof of the whole verifier.

**Known completeness limits:** foreign extension of sealed traits, universal rather than composition-specific open proofs, conservative General(TOP) imports, explicit Closed requirements for reusable rank helpers, type-mention over-approximation, M3 persistent-arrow gaps, M4a virtual-rank gaps, cyclic abstract static bindings, incompatible proof profiles and total-completion rejection of explicit partial/unchecked bodies. User-extensible orders may reduce some rank annotation burden but do not remove these independent requirements.

**Upstream maintenance costs:** the largest changes are stable slot/view metadata through cloning/refinement, interface-only client translation, complete proof inventories across two pipelines, and on-only Std source/resources. The tuple adapter, schema handling and explicit producer evidence should be kept isolated. Avoid a fork-wide redesign of all function values or all cached ASTs inside M4b. Exact-build matching initially trades reuse across upgrades for a simple compatibility policy; later relaxation requires evidence, not a version-string heuristic.

**Executed for this revision:** read the latest issue request/review, inspected the pinned implementation and prior design, checked the Std source counts, and obtained independent reviews of the soundness/modularity rules, artifact boundary and Std migration. Before publication, check the Markdown diff, reference/path validity, internal test/section identifiers and that the PR changes only this document relative to its target. No candidate implementation, Dafny/Z3 run, solver census, runtime experiment, certified Std build or off-mode binary comparison was performed. Prior executed reproducer results are explicitly attributed to the earlier PR revision.

**Recommendation:** replace A2 with declared openness **and stable termination-effect contracts**, reusable producer records, and strict artifact compatibility. These additional interface rules are necessary to make the requested body-only replacement theorem true. Implement the core boundary first, measure the actual migration, and keep incomplete Std/callback support explicit.

## References and pinned source map

The request, prior experiment and decisions are in the linked issue/PR comments. Source links below point to the inspected target commit; symbol names in section 11 are implementation anchors rather than promises that current line numbers stay fixed.

* [Modular termination analysis and classifier][mt]; [bound VC generation][decreases]; [module/trait resolution][resolver]; [export cloning][scope-cloner].
* [`.doo` format and validation][doo]; [input loading][dafny-file]; [library backend][library-backend]; [build command][build-command]; [synchronous build pipeline][sync]; [modern compilation][compilation].
* [Current specification][spec]; [census script][census]; [census workflow][census-workflow]; [fork rules][rules].
* [Std Actions][actions]; [GenericAction][generic-action]; [Frames][frames]; [TerminationMetric][metric].

[request]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6787558931
[pr244]: https://github.com/erniecohen/dafny/pull/244
[old-design]: https://github.com/erniecohen/dafny/blob/339df4cbe954460c7c25a6ae94299c3bebeb927a/docs/dev/design/211-m4b-library-openness.md
[cache]: https://github.com/erniecohen/dafny/issues/241#issuecomment-6778375761
[orders]: https://github.com/erniecohen/dafny/issues/245
[status-m3]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6761674143
[ecoop]: https://drops.dagstuhl.de/entities/document/10.4230/LIPIcs.ECOOP.2015.664
[toplas]: https://doi.org/10.1145/3210258
[mt]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/Resolver/ModularTermination.cs
[decreases]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs
[resolver]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/Resolver/ModuleResolver.cs
[scope-cloner]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/AST/Modules/ScopeCloner.cs
[doo]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/DooFile.cs
[dafny-file]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/DafnyFile.cs
[library-backend]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/Backends/Library/LibraryBackend.cs
[build-command]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyDriver/Commands/BuildCommand.cs
[sync]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyDriver/Legacy/SynchronousCliCompilation.cs
[compilation]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyCore/Pipeline/Compilation.cs
[spec]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/docs/DafnyRef/Specifications.md
[census]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/.github/review/modular-termination-census.py
[census-workflow]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/.github/workflows/issue211-census.yml
[rules]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/AGENTS.md
[actions]: https://github.com/erniecohen/dafny/tree/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyStandardLibraries/src/Std/Actions
[generic-action]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyStandardLibraries/src/Std/Actions/GenericAction.dfy
[frames]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyStandardLibraries/src/Std/Frames.dfy
[metric]: https://github.com/erniecohen/dafny/blob/5f8855a4d9314e37f87d9900682ad71cc8434a55/Source/DafnyStandardLibraries/src/Std/Termination.dfy
