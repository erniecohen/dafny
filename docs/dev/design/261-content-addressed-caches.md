# Content-addressed resolution, translation and verification caches

Design and executable specifications for [issue #261][request]. AI-assisted by
ChatGPT. All implementation references pin Dafny
`428e348b0b125cb67505f03a0026b3f497f98e48` and Boogie
`45a8869bb43456f126577f5ba6fd24725ed6e4d9`
(`v3.5.5+review.c5a9f2ef`). The earlier [pipeline study][pipeline] and
[front-end inventory][frontend] are inputs, not substituted source pins.

This PR adds a design, two C# **proposal patches**, runnable Python models and an
option-audit baseline. It does not modify product C# files or enable a cache.
The proposal patches apply to the Dafny pin; their input capture, typed codecs,
driver wiring and full integration tests remain implementation work. No C# build,
Dafny execution, solver test or performance experiment was run for this study.

## Findings, first

1. **The requested unconditional CSV/JSON byte equality cannot hold:** runtime fields, duration-based CSV ordering and generated timestamp filenames vary even between two cold runs; storing old times does not reproduce the counterfactual current cold run. [JSON][json], [CSV][csv]
2. **An import-only resolution key does not determine translation:** modular termination reads downstream trait implementations and whole-program reachability; finalize the module key with its current analysis projection after loading/resolving local modules. [Analysis][mod-analysis], [translation read][mod-read]
3. **Pruned Boogie alone is not a complete task key:** ordered `UsedTypes` comes from all non-implementation declarations of the translated program plus the current implementation. [UsedTypes][used-types], [Checker][checker]
4. **The authoritative late key should describe the complete solver interaction program:** initialization, actual lowered context, adaptive response policy, limits, seeds, solver environment and decoding schema; a first-query SMT log is insufficient. [SMT interaction][interactive]
5. **Timed successes must be excluded as well as timeouts:** Dafny defaults to a 30-second budget, and skipping hits also perturbs later misses if solver histories are shared. [Default budget][boogie-options], [process-history evidence][review]
6. **`CloneId` cannot simply be replaced in the existing live-object cache:** persistent results need typed graph snapshots, rebinding, effect replay and renewed global admission. [Cache][caching-resolver], [result type][module-result]
7. **The existing builtins hash is not sufficient:** it omits bitwidths and tuple lengths and can be memoized before the catalog stops growing. [Builtins][builtins]
8. **History-free translation needs ownership changes beyond `CurrentIdGenerator`:** declaration generators, variable/label/let/assert/fuel state, match metadata, ordered catalogs and verification provenance all matter. [Declaration][declaration], [match mutation][match]
9. **Proof-dependency reports must remain module-scoped:** combining imported-function dependencies from unrelated translated modules changes potential/uncovered sets. [Dependency manager][proof-manager], [JSON][json]
10. **Early cutoff is an equality theorem:** discard resolution provenance from the late key, preserve the actual execution envelope, and bind the result to current source metadata; do not hash all dependency keys into every proof task. [Current checksum cache][b-cache]

## Contract and notation

The owner's per-module resolution, resolution-key → Boogie-hash table, task-result
table, early cutoff and opt-in decisions are retained. The stage decomposition is:

| Identity | Meaning | Use |
|---|---|---|
| `L(m)` | Local source, options, producer, lexical/builtin/prepass inputs and local keys of all semantic resolution prerequisites | Load the detached pre-global module snapshot; resolve misses in dependency order. |
| `G(m)` | The current whole-program analysis/postpass facts actually observable by this module's translation | Computed after current global passes; includes relevant modular-termination answers and postpass-generated declarations. |
| `R(m)=H(L(m),G(m))` | Finalized module resolution identity, including translation context/profile | The owner's authoritative resolution → Boogie table key. No recursive reverse dependency is added to the local resolution DAG. |
| `B(m)` | Exact printed Boogie bytes and required materialization sidecars | Stored under content digests; names depend only on the finalized input, not translation history. |
| `K(t)` | Complete independent task execution descriptor | Reuse solver evidence if eligible; contains no blanket `L`/`R` salt. |
| `J(t)` | Current report binding, renderer and presentation settings | Re-render a proof hit at today's source locations; optionally cache rendered bytes separately. |

The auxiliary `L` snapshot is necessary because some facts needed by translation
are available only after all local modules are resolved. It lets an unchanged
module avoid body resolution even when `G` changes. Requiring a single pre-global
import-DAG key to determine all translation would be unsound at the pin; the
layered implementation keeps the requested final `R → hash(B)` table.

Proposed interface: `--content-addressed-cache <directory>`, absent by default;
help text: “Reuse compatible local resolution, translation and verification
records in this directory. Unsupported configurations use ordinary execution.”
Register it for CLI and IDE/project options only when the corresponding stage is
ready. Cache health/hit telemetry uses an explicitly requested diagnostic channel,
not ordinary stdout/stderr. Do not silently clear the user's timeout, change a
seed, enable a Boogie semantic option, or turn a previous failure into success.

The output reference is **cache-enabled cold execution with the same options and
an empty table**. History-free names can change RU or resource-limited outcomes
relative to legacy naming; no ON-versus-OFF equality is inferred from alpha
equivalence. OFF keeps the pinned pipeline. If “no reported result may change”
also requires ON to equal every history-dependent legacy run, that requirement
conflicts with eliminating the demonstrated history dependence and is not
established by this design. [Front-end measurements][frontend], [ordering][checker]


## 1. Proof-task key and execution contract

For the strict current-output contract, bypass proof-result reads/writes for
time-sensitive verdicts, progress/timing output, current CSV/JSON/TRX/text
performance logging, process-history-sensitive execution, unmodeled callbacks,
or requested side effects that cannot be faithfully reconstructed. Resolution
and translation caches can still be useful where they preserve the selected
protocol. A fallback preserves existing semantics; it does **not** establish
the impossible claim that two independently timed cold executions print equal
bytes.

An optional later deterministic logger mode can retain all diagnostic/RU fields,
use stable ordering and a specified sentinel/omission for time on **both cold and
warm** paths, with actual timing separately reported. That changes the logger
contract and needs an explicit design decision; the design must not silently
reinterpret `runTime` as producer time. Byte equality for the existing general
CSV/JSON requirement is therefore a recorded incompatibility, not a promised
implementation milestone.

The mathematical statement is conditional on a deterministic supported
solver/adapter/profile, complete task admission, complete input capture and a
trusted record producer. No source-only audit proves the determinism of arbitrary
Z3 builds, plugins, multithreaded tactics, timeouts or machine failures.

### Authoritative late identity

Use a length-framed domain-separated cryptographic digest, versioned encodings,
and exact byte/structural equality on collision suspicion:

```text
execKey = H("dafny-task-v1", adapterABI, solverEnvelope,
            initialCommands, responseProgram, residualEffectiveOptions,
            labelAndModelDecodeSchema)
renderKey = H("dafny-render-v1", execKey, rendererABI,
              currentReportBinding, effectivePresentationOptions)
```

The result table is keyed by `execKey` and stores typed solver/result evidence;
`renderKey` is needed only if rendered bytes are memoized. Locations that never
reach the solver are rebound from the current compilation when rendering. A
changed source range can therefore reuse the solver result without replaying a
stale diagnostic. If a source path or ID enters the commands, it participates in
`execKey` exactly; do not pretend it is report-only.

`initialCommands` includes the ordered fresh-process initialization, prelude /
effective lowered background, all declarations and casts, query-local generated
axioms, naming, options and the first check. `responseProgram` is a versioned
finite interaction program: parse result; request reason/RU/core/model/path;
block a reported assertion and iterate under the exact error-limit policy;
terminate and clean up. Its code and immutable parameters are hashed before the
first solver call. A recorded transcript is output evidence; using the *saved*
response-dependent transcript as the *new* key would be circular.

This is preferable to canonical pruned Boogie as the **authoritative late key**:
it captures lowering, whole-program `UsedTypes`, name allocation, erasure,
literal sharing effects, triggered instances, solver options and order exactly.
A pruned-Boogie descriptor is a useful pre-key/index, not authority until a
proved deterministic lowering theorem covers all those inputs. Do not hash only
the last `(assert ...)`, or only the first `(check-sat)` prefix.

Do not normalize the final SMT string after generation: alpha-renaming, axiom
sorting, duplicate elimination, trigger sorting, whitespace changes inside quoted
strings or symbol rewriting can change behavior or model/error decoding. Any
normalizer must run **before actual cold execution as well as hashing**, with
the corresponding bijective decode map. Ordinary transport framing may be
canonicalized only where both execution paths consume the same framed commands.

### Item-by-item disposition

`KEY` = included in execution identity; `PLAN` = represented by exact lowered input
and not independently salted; `REPLAY` = stored/rebound/replayed; `REJECT` = bypass
until modeled. No item defaults to
"ignore".

| Input/effect | Disposition and reason |
|---|---|
| All pruned declarations, top-level stable order, reorder/shuffle result, ties | **KEY/PLAN** in commands; no set hashing or renumber-only comparison. |
| Hidden/revealed function definitions, independent axioms, `{:keep}`, function/type/datatype and constant axioms, triggers, weights, fuel, all generated assertions | **KEY/PLAN**; use actual split pruning/lowering, not a call-graph dependency approximation. |
| Ordered `UsedTypes` from the unpruned program | **KEY/PLAN**; required before casts/type axioms are generated. |
| Dafny prelude snapshot, arithmetic preprocessor defines; Boogie background text and `ProverPreamble` include closure | **KEY/PLAN**; hash file bytes, not path/mtime; expand or snapshot solver-side includes and nested relative resolution, else **REJECT**. |
| VC, split control-flow map, implementation contracts, inlining/passification effects, selected assertions, path/focus origin | **KEY/PLAN** for execution; **REPLAY** for current reporting inventory. Every current selected task must remain represented. |
| Exact initialization/reset/restart and process history | **KEY** protocol; require fresh identical histories. Merely hashing old history does not preserve the histories of following misses. |
| Seed, declaration/name shuffle seed, per-implementation override, per-split/per-iteration derived seed | **KEY/PLAN** actual values; never only root CLI seed; allocate derived seeds before scheduling. |
| Ordered Boogie and solver pass-through options, duplicate options, effective values after module/declaration overrides | **KEY/PLAN**; never sort or collapse ordered last-wins command sequences. |
| `{:id}`, proof-dependency `idN`, named assumes, labels, control-flow IDs, model names | **KEY/PLAN** if emitted; **REPLAY** stable associations and structured component tuples. Canonicalize only before cold lowering, with a total inverse map. |
| Source filenames/URIs, line/column/end ranges, token text/trivia, related locations, asserted expressions, diagnostic descriptions | **REPLAY** current report binding; **KEY** in render key; also **KEY/PLAN** whenever embedded in executable commands. Missing/ambiguous binding is a miss. |
| Actual loaded Dafny/Boogie/serializer/renderer/rewrite/plugin bytes | **KEY** at the stage they execute; release labels and git SHAs alone are metadata. Execution adapter/decoder ABI belongs in `execKey`. |
| Solver executable, reported version, solver dialect/factory, invocation argv, libraries/interpreter/container identity | **KEY**; same version string at same path is insufficient. Unknown external dependencies => **REJECT**. |
| OS/architecture/runtime, solver-relevant environment, working directory for includes, culture/encoding/newline and locale | **KEY** where execution depends; **REPLAY/KEY** for presentation. Freeze declared relevant environment; unknown solver/plugin reads => **REJECT**. |
| Resource/error/memory limits and bounded reasoning mode | **KEY** exact effective values; no inference from saved RU to a new limit. OOM itself **REJECT**. Bounded results never count as unrestricted proof. |
| Time limits from Dafny, Boogie, prover flags, SMT options, environment; timeout diagnostics/keep-going/deadlines | **REJECT** even for a previously successful result. Time budgets make the counterfactual outcome variable. |
| Coverage/vacuity warnings, model extraction, proof obligation expressions, warning policy and warnings-as-errors | **KEY** if they affect execution; **REPLAY/KEY** rendering and exit aggregation. Empty/missing coverage is not interchangeable. |
| Current global admission and selected task manifest | Recompute before accepting/reporting hits; **REPLAY** current aggregate, never reuse a module success summary as coverage of today's tasks. |
| Completion order/progress/statistics, current duration/timestamps, auto-generated log names | **REJECT** strict unchanged renderer; deterministic output policy is a separate specified contract. |
| Cache directory, eviction quota, record creation/access time, transport buffers | Exclude from proof identity after proving no program/report effect; telemetry outside equivalence channel. |
| Source/resolution hash of the containing module and all its dependencies | **Do not salt into the late task key**; retain as provenance in the translation table. Otherwise early cutoff is lost. |
| Arbitrary callbacks, custom solver factories, plugin effects, filesystem/process output side effects | **REJECT** unless typed immutable inputs and deterministic event/artifact replay are registered. Delegate addresses/type names are not a sound fingerprint. |

The source anchors for pruning and hidden definitions are [Pruner:61–97](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/Prune/Pruner.cs#L61); initialization and options are [PrepareCommon/SendVCOptions:340–453](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Provers/SMTLib/SMTLibProcessTheoremProver.cs#L340).

### Every option: an enforceable total rule

The source baseline [261-option-audit.tsv](261/261-option-audit.tsv) lists **761 audit occurrences**, not
761 distinct user options: 160 Dafny `Option` declarations, 49 Dafny legacy switch
cases, 102 Dafny effective-member occurrences, 98 Boogie legacy switch cases,
and 352 Boogie effective-member/interface occurrences. Every row has a pinned
source anchor and conservative `KEY` or unmodeled-adapter rejection disposition.
[option_inventory_261.py](261/option_inventory_261.py) regenerates it; it is intentionally an inventory aid,
not a C# parser or evidence that runtime coverage is complete.

The implementation must replace the informal "options" field by a generated,
versioned `EffectiveOptionsSnapshot`: enumerate **all** registered modern options
(defaults included), inherited Dafny/Boogie effective option properties/fields,
the full parsed prover-options object, module/declaration overlays, plugins and
ordered raw tokens/referenced files. A build test requires each enumerated member
to have exactly one rule: `ResolutionKey`, `LoweredIntoPlan`, `ExecutionKey`,
`CurrentReportBinding`, `ProvenTransportOnly`, or `Reject`. Fail a build when the
registry changes without a rule; reject reuse at runtime for unknown dynamic
options or values not representable by the schema. Include exact floating-point
bits, list order, nullable distinctions and enum values. Hashing `ToString()` on
arbitrary objects is forbidden. Registry scope `Cli` is **not** evidence an option
has no proof effect: time/limits/solver/error/isolation options have that scope.
[Dafny OptionRegistry](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Options/OptionRegistry.cs#L15), [BoogieOptionBag:160–173](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Options/BoogieOptionBag.cs#L160).

Initially keep every serializable effective option in the relevant full stage
snapshot unless its complete effect is already represented in the lower plan;
this is conservative over-keying, not a claim of maximal option-change reuse.
For full early-cutoff completeness, upstream-only fields must be removed from
the **late** key after the complete plan is formed; a complete frozen task must
not read the mutable original `DafnyOptions` again. A narrow immutable executor
interface makes the completeness claim checkable.


## 2. Cached result and exact replay

```text
TaskResultV1
  schema / key / digest of immutable execution descriptor
  producer trust namespace + exact adapter/solver identities
  normal termination class + raw solver terminal responses/reasons
  outcome / boundedness / exact effective resource limit / exact RU
  ordered iteration and retry outcomes; raw transcript for audit
  complete assertion inventory / stable label-schema digest
  counterexamples[]:
    assertion kind + label, path block labels, call/return/loop details,
    ordered model observations + model-view/source-variable binding schema,
    related proof-obligation labels, diagnostic metadata
  covered components[]:
    structured tracked-node kind and member labels, in observed order
  assertion outcomes[] + full error-limit/truncation state
  terminal diagnostic events[] + deterministic solver warnings with channel/order
  required side-effect artifact descriptors if supported
  separate observation record: producer timings/provenance (never current elapsed time)
  length/checksum/provenance; no serialized executable objects or delegates
```

Materialize `VerificationRunResult` into the current task's `Asserts`, source
tokens, split number/iteration and proof dependency manager; do not store pointers
to prior AST objects or a split index as the entire association. The pinned record
has `VcNum`, `Iteration`, `StartTime`, outcome, runtime, error limit,
counterexamples, asserts, covered elements, RU, solver and pruned declarations.
[Split.ReadOutcome:895–925](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/Splits/Split.cs#L895).

For negative results the failing assertion alone is insufficient: preserve
assert/call/return counterexample type, source and related sites, ordered model
trace, instantiated contract, assertion description and optional asserted
expression; use the current renderer after rebinding. For coverage, preserve
structured `TrackedCallRequiresGoal/Assumed`, `TrackedCallEnsures`, invariant
established/maintained/assumed and labeled components, then join through the
**current** manager. Preserve defined ordering and reconstruct uncovered elements
against the current potential dependency set. [ProofDependencyManager:66–96](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/ProofDependencyManager.cs#L66).

Recompute stdout/stderr messages and exit aggregation from the current full
result stream; replay a task event once, in declared source/split order, never
append cached stdout as an independent stream. Stored RU remains proof cost;
actual executed work belongs only to separately requested telemetry. Warning
policy, current resolution errors, partial failures, logging I/O errors and
cancellation can still make the invocation fail; a saved exit code cannot replace
current aggregation. [VerifyCommand:172–227](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyDriver/Commands/VerifyCommand.cs#L172).

| Terminal observation | Reuse rule |
|---|---|
| Normal unsat/valid | Reuse under the complete deterministic profile and exact limits with all requested report/coverage payload. |
| Normal sat/invalid | Same, once model/path/description replay is implemented; verify raw class was normal sat, not a collapsed unknown. |
| Deterministic resource exhaustion | May reuse at the **same** limit under a qualified deterministic RU protocol with complete terminal/report payload; never extrapolate to another limit. |
| Deterministic unsupported/unknown | A later adapter can certify and replay; initial schema rejects all such observations because the enum conflates reasons and callbacks. |
| Wall timeout, deadline, user cancellation, timeout-driven retry/diagnostics, resource termination ambiguously reported as canceled with a time limit | Never reuse. |
| OOM, crash, unexpected output, process recovery, exceptions, partial record, incomplete diagnostics/coverage/model, unmodeled callback | Never reuse. |
| Bounded success / smoke test / old cache-injected assumptions | Separate result kind and protocol; not admissible as normal proof; initial implementation rejects. |

The first implementation may support valid results only, but that is a milestone,
not a claim to satisfy final hit completeness. The final supported profile needs
normal invalid and deterministic resource-limit results or a documented residual
gap. Adaptive keep-going is a separate composite task: key its branching policy
and child inputs and replay the same deterministic tree. Existing timed
keep-going/timeout diagnostics must bypass, not splice a cached leaf into an
unmodeled timed parent. [SplitAndVerifyWorker:138–163,208–229](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/Splits/SplitAndVerifyWorker.cs#L138).


## 3. History-free translation and C# proposal

[history-free-translation.patch](261/patches/history-free-translation.patch) applies against the Dafny pin and changes 13 files; it is a proposal stored as an artifact, not a product change. `TranslatorFlags.HistoryFreeInput == null` selects all existing bodies. The integration supplies a non-null checked manifest only under the eventual cache option. No new CLI option is registered by this patch.

The patch introduces a synchronous, thread-local `TranslationLocalState` around `DoTranslation`; redirects `Declaration.IdGenerator` at its source; redirects **both reads and writes** of `UniqueName`; isolates label, assert-label, let and fuel state; uses independent proof-dependency state with stable logical-module prefixes; replaces module/catalog/selection reads with manifest projections; and computes missing match cases locally. It does **not** prefix solver names with `ResolutionKey`. The logical-module prefix uses injective UTF-8 hexadecimal encoding and `_`, because Boogie's compound proof labels reserve `$`. [TrackedNodeComponent.cs](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/TrackedNodeComponent.cs#L63).

The patch exposes `CompletedModuleProofDependencies` after translation, and never merges the local manager into the program manager. The integration must freeze its id/source maps and carry that manager through `IProgramVerifier`'s returned module-task object, `Compilation.HandleStatusUpdate`/`BoogieUpdate`, and `CanVerifyResult.ProofDependencies`; the existing JSON logger already prefers that result-scoped manager. Global unions are not authoritative for task potential/uncovered elements: importing the same function from another translated module must not enlarge the current task's potential-dependency set. This carrier integration is explicitly unfinished in the proposal. [Compilation.cs](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Pipeline/Compilation.cs#L548), [JsonVerificationLogger.cs](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyDriver/JsonVerificationLogger.cs#L143).

### Audit: every prior Dafny translation inventory row, plus pin-specific additions

`P` means covered by the proposal patch; `I` means the integration must enforce the stated precondition; `N` means the source is already per-translator or does not reach output in the admitted profile.

| Row | Source at the pin | Required change / status |
|---|---|---|
| T1 | [NonglobalVariable.cs:35–40](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Expressions/Variables/NonglobalVariable.cs#L35), [LocalVariable.cs:61–67](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Statements/Assignment/LocalVariable.cs#L61) | P: source-variable name dictionaries belong to this translation; active reads never fall back to a legacy AST memo. |
| T2 | [Declaration.cs:173](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/TypeDeclarations/Declaration.cs#L173), [FreshIdGenerator.cs:10–62](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/FreshIdGenerator.cs#L10) | P: declaration getter returns a translation-local fresh generator, covering direct field readers as well as `CurrentIdGenerator`; invariant numeric formatting in ON mode. |
| T3 | [FreshIdGenerator.cs:14–27](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/FreshIdGenerator.cs#L14) | P: push/pop stacks are owned by the new generator; no reset/copy of a shared stack. |
| T4 | [Methods.cs:509](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs#L509), [Functions.cs:662](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L662), [ExpressionTranslator.cs:493](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L493) | P: imported-function contexts use the same scoped declaration getter; nested `$fh$`, `$finiteMap#`, `$l#`, `$lambdaFamily#`, `$reads#` counters are no longer shared. |
| T5 | [Label.cs:8–15](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Statements/ControlFlow/Label.cs#L8) | P: memoize labels in the translation, including labels first named by the translator's default generator. |
| T6 | [LetExpr.cs:24–35](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Expressions/Variables/LetExpr.cs#L24) | P: desugarings live in the translation scope; no AST `lastBoogieGeneratorUsed` mutation in ON mode. |
| T7 | [AssertLabel.cs:8](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Statements/Verification/AssertLabel.cs#L8), [Reveal.cs:20](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Reveal.cs#L20) | P: generated Boogie expressions live in a scoped map, not source labels. |
| T8 | [ProofDependencyManager.cs:17–40](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/ProofDependencyManager.cs#L17) | P/I: independent module manager and stable logical-module namespace; retain the completed manager on the module-task/result carrier; preserve the local id-to-source sidecar, never merge it for task reporting. |
| T9 | [ProofDependencyManager.cs:28–45](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/ProofDependencyManager.cs#L28) | P: module owner starts unset; fail closed if an emission has no owner, never inherit the preceding module's owner. |
| T10 | [VisibilityScope.cs:28–50](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/VisibilityScope.cs#L28) | P/I: bypass the shared mutable memo in ON mode; declaration/export scope sets must already be frozen. |
| T11 | [Types.cs:36–76](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Types/Types.cs#L36) | P: begin/end scope cleanup also runs on failure; no `await` in the active translation scope. |
| T12 | [BoogieGenerator.cs:4529,4790](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L4529) | P: an ON translation has its own fuel stack, never an inherited mutable `AsyncLocal` value. |
| T13 | [SystemModuleManager.cs:15–19,72](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/SystemModuleManager.cs#L15), [BoogieGenerator.cs:773,802,2518](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L773) | P/I: manifest supplies canonical widths/declarations; object/arrow/ordinal/tuple lookups must be rebound to the checked builtin catalog and must allocate no late builtins. |
| T14 | [IllegalTriggerRemover](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L901) | I: provide a module-local reporter; record ordered structured diagnostics and flush in canonical module order, including translation-cache hits. |
| T15 | [BoogieGenerator.cs:755–757](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L755) | I: route verbose `OutputWriter.Status` through the same ordered event sink; cache statistics must use a separate explicitly requested channel. |
| T16 | [BoogieGenerator.cs:759–763](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L759) | P/I: require the exact pinned `DefaultPlugins` list; integration bypasses caching for additions/reordering. `Plugins.Any()` would wrongly reject all runs because defaults contain two built-in plugins; serial execution alone does not make custom plugin state cacheable. |
| T17 | [Declaration.cs:114–130](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/TypeDeclarations/Declaration.cs#L114), [LocalVariable.cs:83](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Statements/Assignment/LocalVariable.cs#L83) | I: discard backend/name memos at persistence boundary; materialize only resolved type fields; prefreeze remaining source-derived caches under the fingerprinted options. Some `??=` sites allocate type proxies and are not all benign strings. |
| T18 | [TranslatorFlags](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L85) | I: immutable effective options plus translator flags, prelude bytes and tool fingerprint in the manifest. |
| T19 | [RequireCardinalityValidation](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1011) | N/I: rerun whole-program admission before lookup/use; never import an old successful receipt as authority. |
| T20 | [Boogie Boolean singletons](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/AST/Expression/AbsyExpr.cs#L485) | N: same Boolean types on this path; exclude mutable metadata from sharing and test concurrent preparation separately. |
| T21 | [BoogieGenerator.cs:143–155,2005–2032](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L143) | N: one generator per module; do not persist `functionHandles`, Boogie nodes or pointer-keyed caches. |
| T22 | [Boogie QuantifierExpr.SkolemId](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/AST/AbsyQuant.cs#L585) | I: admit `EmitDebugInformation=false` initially; general support needs Boogie per-task skolem identities. |
| H23 | [MatchVerifier.cs:87–123,174](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/Statements/MatchVerifier.cs#L87) | P: compute missing-case lists locally in ON mode; preserve the old mutation path in OFF. |
| H24 | [BoogieGenerator.cs:837,1260,1331](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L837) | P/I: use the same ordered observable module projection for declarations, fuel and trait-parent axioms; declarations retain source order within each projected module. |
| H25 | [ShouldVerify](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/ShouldCompileOrVerify.cs#L37) | P/I: manifest includes declaration verification membership and include/library provenance; imported-axiom visibility and implementation selection remain distinct. |
| H26 | [capture ordering](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L1600) | P/I: ON string ordering is ordinal; unresolved ties must retain deterministic AST traversal order; runtime/culture formatting remains fingerprinted. |
| H27 | [map projection cache](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.cs#L4404), [Substituter.cs:1093](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Substituter.cs#L1093) | N/I: backend projections are already local at this pin; serialize only the source-identity alias relation, never cached `Bpl.Function` objects. |
| H28 | [function-SCC height](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.SplitExpr.cs#L494) | I: freeze/serialize relevant call-graph SCC relations and ranks; they affect inlining, independently of names. |
| H29 | [TokenTextWriter.cs:135–161](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/TokenTextWriter.cs#L135) | N/I: wrapping follows identifier widths; no whitespace stripping or regex renaming; content-address the exact fixed-encoding printed bytes and preserve pretty-print settings. |

| H30 | [ModularTerminationAnalysis.Of/program traversal](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModularTermination.cs#L220), [translation read](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs#L150) | **Critical I:** finish global analysis, freeze/hash module-specific `OpenTraits`/`ReachesOpenDispatch` answers and all translation-observed global contributions as `G_m`, then finalize `R_m = H(L_m, G_m)`; the proposal does not implement this projection and must remain disabled until it is supplied. |
| H31 | [RunAllTestsMainMethod.PostResolve](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Rewriters/RunAllTestsMainMethod.cs#L96), [SetBody](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Rewriters/RunAllTestsMainMethod.cs#L239) | I: rerun the global pass and include its final generated-body contribution in the owning module artifact; it enumerates compile modules, test attributes/options and signatures. Its generated main is `{:verify false}` at this pin, so it is not evidence of an additional verified VC, but caching the final resolved AST must still preserve the body. |

#### The 29 AST `??=` sites, classified

The source audit found 29 occurrences, the same count as the earlier inventory; counting occurrences is not a proof of purity.

- Generator-dependent variable `uniqueName` (two): redirected by P/T1.
- Code-generator `compileName` (two), `UserDefinedType.compileName`, `Formal.CompileName`, `Declaration.compileName/enclosingModuleName`: no compile cache enters a verification artifact; when read, require the same effective backend/options or recompute.
- `sanitizedNameShadowable` (two), declaration/tuple sanitized names: source-only strings after rebinding.
- `safeSyntacticType` (two), `IdPattern.Type`, `LetExpr.Havoc`'s local type: require resolved type data; never reconstruct by allocating a fresh unbound proxy during loading.
- Module `visibilityScope`: must be created/frozen during resolution; no translating worker may allocate scope identities lazily.
- `Type.Scopes`: managed by the thread-local translation scope.
- Remaining `??=` values are locals or temporary collections: tuple `argumentGhostness`; arrow formatter; newtype walk-set; datatype visitation set; four cycle-analysis collections; assert-guard local; export-set-name local; attributes result list.

### Boogie preparation boundary: prior B1–B12

These are a separate contract from Dafny translation. The proposal intentionally does not patch Boogie or claim safe parallel preparation for every Boogie option.

| Row | Source | Action |
|---|---|---|
| B1 | [Absy.UniqueId](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/AST/Absy.cs#L53), [monomorphization](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/Monomorphization.cs#L577) | Initially admit `TypeEncoding.Arguments`; generalize by program/task-local identities for emitted monomorphization names. |
| B2 | [SkolemId](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/AST/AbsyQuant.cs#L585), [SMT skolemid emission](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Provers/SMTLib/SMTLibLineariser.cs#L476) | Initially disable debug-information profile; ultimately assign local skolem IDs before both hashing and solving. |
| B3 | [TypeProxy.proxies](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/AST/AbsyType.cs#L1953) | Preparation errors bypass result caching; interlocked increments fix a race but do not make diagnostic text history-free; stable diagnostic-local numbering is needed for cached errors. |
| B4 | [bvtypeCache](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/AST/AbsyType.cs#L380) | Serialize/prewarm required types initially; later use safe static initialization and immutable interned types. |
| B5 | [Split.currentId](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/Splits/Split.cs#L62) | No emitted dependency found: the increment's result is unused. |
| B6 | [labelToTypes](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/QuantifierInstantiationEngine.cs#L54) | Exclude `{:pool}` initially; scope the pool table to the task/program before supporting it. |
| B7 | [print-option mutation](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/VerificationConditionGenerator.cs#L452) | Snapshot options and serialize optional print/trace side effects; `--bprint` before preparation can be rendered from the artifact. |
| B8 | [TypeEncodingMethod write](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/ExecutionEngine/ExecutionEngine.cs#L512) | Not on `GetVerificationTasks`; freeze the actual prepared effective setting nevertheless. |
| B9 | [implementation cancellation map](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/ExecutionEngine/ExecutionEngine.cs#L82) | Request/task instance IDs stay transient; do not use cached logical IDs as cross-request cancellation keys. |
| B10 | [LambdaHelper](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Core/LambdaHelper.cs#L151) | Per visitor; deterministic input order remains required. |
| B11 | [implementationData](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/VerificationConditionGenerator.cs#L35) | Keyed by implementation objects; serialize values, never the weak table or object identity. |
| B12 | [CheckerPool](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/CheckerPool.cs#L12) | Solver-process history must be independently eliminated or included in the task envelope; it is not repaired by deterministic Dafny translation. |

### Conditional determinism argument

Let `L_m` be the DAG-composed preglobal resolution key. Re-run global passes after assembling those cached local results. Let `G_m` be a canonical projection of every global result that translation of `m` can observe, including modular-termination open-trait and reachability classifications, body mutations and generated declarations. Define the final artifact/translation key `R_m = H(L_m, G_m)` only after those passes. `G_m` is derived from the assembled program, not from recursively adding downstream modules as reverse prerequisites; therefore this two-stage construction does not introduce a cycle in the resolution DAG. Conservatively hashing the complete global output is sound initially; narrowing to exactly the read projection requires read-trace or dependency-closure validation.

Let `I(m)` contain the source-derived immutable resolved projection, `G_m`, builtin catalog, verified-declaration membership, options, prelude and versions; the final resolution/translation key must identify all of `I(m)`. `ModularTerminationAnalysis.Of(program)` must be freshly computed for the assembled program and frozen before it supplies `G_m`; never reuse its `ConditionalWeakTable` value after mutating that program. A method-body-only edit is not automatically invisible when it changes `ReachesOpenDispatch`. Start translation with fresh local state. Induct over the deterministic traversal: a pure read is fixed by `I(m)`; every name/memo read or write is determined by earlier events of this translation; each emitted declaration therefore has the same bytes and position. At completion discard local state and publish the artifact and its sidecars atomically. Other modules' translation histories cannot enter this induction. A collision-free, stable logical identity is enough for global reporting labels; injectivity of `I(m) -> generated name` is unnecessary.

The proof is conditional on the integration obligations, not evidence that the patch implements all of them. In particular, a global builtin catalog in every `I(m)` is sound but makes adding an unrelated builtin invalidate every module; a per-module catalog requires a proved closure construction. Include every actually emitted visible/revealed declaration, and use the same projection for fuel and trait-parent axioms. An ordered input audit should reject any access outside this manifest during an instrumentation test.


## 4. Resolution prerequisites, keys, file format and C# proposal

For each declaration vertex `m` of the existing acyclic module graph, use:

```
L(m) = SHA256(frame("dafny/local-resolution-input/v1",
  exact producer and codec identity, resolver engine,
  logical module/facade identity,
  immutable owned source and parsed-structure digest,
  lexical binding/environment digest,
  effective option manifest,
  source-origin/reporting projection,
  verification/compilation-membership projection,
  program-PreResolve contribution,
  builtin-definition manifest,
  sorted [(edge role, source slot, selected target/view identity, L(target))]))
```

Every field has a type tag and explicit byte length; integers have a specified endianness. Lists carry counts. Map entries use ordinal key order; source order is preserved inside semantic lists. Dependency edges are sorted by `(role, source slot, target/view identity)`, not by GUID, encounter order or hash alone. Preserve multiplicity where imports refer to the same target under different aliases/views. Schema/build hashes are domain-separated. A digest collision is outside the ordinary cryptographic assumption; stored full input bytes are compared at lookup to detect accidental misaddressing and permit collision quarantine. Source-file access, options and snapshots are held immutable during a compilation; a change creates a new compilation generation.

`owned source` means the module's own syntax/trivia and declaration structure, before mutation. A parent owns a nested module's declaration slot/name/attributes and refers to the nested body by a child handle; hashing the whole enclosing file unnecessarily invalidates unrelated modules. Default-module fragments from several files retain their ordered file identities and exact owned spans. Include expansion, `.doo` program text, command/project/module options, source roots, `alreadyVerifiedRoots`, `alreadyCompiledRoots` and source URI policy are represented by the parts that this module can read. Initially it is acceptable to include a conservative whole-input manifest while the projection is audited; it remains sound but gives fewer hits. Do not hash `CloneId`, AST addresses, reporter objects or the entire transitive CLR object graph.

Freeze the source and binding inputs before any worker resolves a module. After `ProcessPrefixNamedModules`, dependency binding and all `PreResolve(Program)` passes have run, freeze the module-local prepass contribution as well; it covers expanded attributes and a generated test-runner declaration. The visitor must omit resolved backedges and replace child bodies by handles. Do not hash a module after a dependency result has replaced pointers in it. Record original and generated identities separately. `SourceMap` is either exact current origin bytes (v1, conservative) or a versioned relocatable mapping with structured diagnostics (later); a positional edit must never replay yesterday's line number.

### Prerequisite set, exactly

The local-key implementation uses the transitive closure induced by the current resolver graph, retaining every direct edge below; it does not substitute an ordinary source-import graph ([graph construction](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ProgramResolver.cs#L125-L154), [edge cases](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ProgramResolver.cs#L254-L389)).

| Input relation | Required edge/input |
|---|---|
| Literal module with an alias import | Parent → alias vertex; alias → lexically selected root after `SkipEnclosingModules`; include the remaining qualified path, `opened` bit, alias name and ordered selected export sets. The root's own graph covers nested target modules. |
| Abstract import/facade | Parent → abstract-import vertex → selected original root/view; include facade identity and visibility. Materialize its separate abstract signature graph; it is not a structural alias for an equal-looking concrete type. |
| Refinement, implementation or replacement base | Literal refiner → actual resolved base root after ancestor skipping; include implementation kind and qualified suffix. A refiner can read/clone the base's implementation, so later observation filtering must preserve that stronger view. |
| Nested literal module | Enclosing literal → nested module, including synthesized prefix-named modules. The child does not acquire a cyclic full-parent dependency; hash the lexical ancestor environment it reads separately. |
| Export declaration | Export vertex → owning literal module. Export `extends` dependencies and export-consistency clones are resolved inside the owner's result; serialize the extends closure, `provides`/`reveals`, default export and visibility. The parent has no extra reverse edge to its own export vertex. |
| Lexical binding | Hash selected bindings and relevant name-table/negative-lookup/ambiguity context, including ancestor-qualified-id option and `RootIndex`; always rebuild binding and cycle checks before cache lookup. An added sibling can change lookup without editing an import's text. |
| Builtins | Universal builtin generator/build/options identity plus the exact structured keys required by the module/view: array rank, arrow arity/flavour, tuple length and entire ghostness vector, bitvector width, and relevant creation/initialization options. |
| Program prepass contribution | Hash generated or modified local syntax and any external input it used; unsupported external plugin or unaudited prepass makes the module noncacheable. |

Builtin interning must become a deterministic map from a structured builtin key to an immutable definition. The refreshed resolver additionally shares `PreTypeBuiltins` and initializes bitvector pretype signatures on first touch ([PreTypeResolve.cs:63–138](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/PreType/PreTypeResolve.cs#L63-L138), [shared state:723](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/PreType/PreTypeResolve.cs#L723)). That initialization must complete before publication and depend only on the builtin key, not the first worker. Replay each module's builtin-demand and visibility-effect journal before dependents run. Do not key on the accidental global set/order left by previously resolved siblings. Until the interner and module projection are complete, a full canonical catalogue digest is a sound, coarse fallback; never use `MyHash` as that digest.

### Passes outside the per-module result

1. Reset `CardinalityValidationReceipt`, initialize lexical scopes, formatter snapshots, prefix-module expansion, binding/cycle analysis, and current scheduling heights.
2. Run each `PreResolve(Program)` pass; capture its local generated contribution before resolving modules.
3. Resolve or load detached module snapshots in dependency order; publish class tables and rebinding effects before dependents run, and diagnostics in canonical source/module order.
4. After all modules succeed, run `InstantiateReplaceableModules`, `CheckDuplicateModuleNames`, every `PostResolve(Program)` pass, then fresh `CardinalityValidator.Validate` with current cancellation state.
5. Only after successful current-program admission may translated tasks or cached verification results be published.

The concrete program postpasses include the auditor, test-runner body generation, modular-termination analysis and plugin hooks, in addition to cardinality ([RewriterCollection](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Rewriters/RewriterCollection.cs), [ModularTermination.cs:677](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModularTermination.cs#L677), [Auditor.cs:120](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Auditor/Auditor.cs#L120)). The reviewed built-in plugins are part of the producer fingerprint (`DafnyOptions.DefaultPlugins` is nonempty); unclassified external `AdditionalPlugins`/`AdditionalPluginArguments` are ineligible. External plugin behavior is not inferred from an assembly hash: arbitrary I/O/time/process state requires an audited purity/input contract; otherwise bypass persistent reuse for affected stages.

`Height` is scheduling metadata, not stable declaration identity. Loaded declarations and synthetic abstract modules must receive the current compilation's bindings/heights; preserve edge/order relations rather than old global integer values in the snapshot. Program-global replacement order still uses the current heights ([ProgramResolver.cs:189–198,232–246](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ProgramResolver.cs#L189-L246), [abstract signatures](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModuleResolver.cs#L935-L1004)).

### Finalized resolution key: the global projection is a real prerequisite

**The ordinary import-DAG key is not sufficient for the fixed resolution-key → BPL table.** At this pin, `OpenTraits(program)` inspects cross-module implementations in every module, including downstream modules that the trait module does not import ([ModularTermination.cs:145–171](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModularTermination.cs#L145-L171)); `ModularTerminationAnalysis` propagates reachability over program bodies and override edges ([237–371](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModularTermination.cs#L237-L371)). Translation calls that analysis and emits different termination checks according to `OpenTraits` and `ReachesOpenDispatch` ([BoogieGenerator.Decreases.cs:143–180](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs#L143-L180)). The analysis need not mutate the AST to be an observable translation input.

Distinguish the auxiliary **local resolution key** `L(m)` above from the **finalized resolution key**:

```
G(m) = canonical per-module post-analysis input projection
R(m) = H(frame("dafny/final-resolution/v1", L(m), G(m)))
local-resolution[L(m)] -> typed local ModuleResolutionResult
resolution[R(m)]       -> local snapshot reference + G(m)
translation[R(m)]      -> exact module BPL hash + sidecars
```

The extra local table preserves the owner's literal `R -> BPL` table while allowing body-resolution hits before current global analyses run. The phase graph is `local dependencies -> local snapshots -> current global analyses -> projected G -> finalized R -> translation`; local prerequisites use **L**, never recursive finalized **R**, so reverse whole-program facts introduce no cycle into the resolver's DAG. This is internal staging, not a new user concept. A changed `G` re-finalizes/re-translates the affected module but need not re-resolve its bodies. A changed unrelated global fact leaves `G`, `R` and the translation hit unchanged.

`G(m)` includes the fixed translation profile, all postpass-generated local AST edits, and every global-analysis query that the module's translator may make, keyed by stable nominal declaration/view identity. For modular termination this means at least open-trait and reaches-open-dispatch answers for the complete potential callee closure, including **negative** answers. Compute those answers from current whole-program analysis; hash answers and semantic identities, not a whole-program source digest. Refactor the translator to consume this frozen view instead of consulting `ModularTerminationAnalysis.Of(program)` behind the key's back. The projection API must reject any unlisted read, expand the view and retry/key it, or bypass caching; absence is never interpreted as `false`. Conservative inclusion of every potential member in the module's translation projection is safe until exact query demand is implemented.

The other reviewed postpasses separate as follows:

| Pass | Effect and final-key treatment |
|---|---|
| Cardinality validation | Explicitly admission only, with no type/code-generation mutation; rerun it and require current success, but do not salt graph counts or the entire program into `G` ([validator:14–16,90–95](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/Cardinality/CardinalityValidator.cs#L14-L95)). |
| Modular termination `PostResolve` | Emits current diagnostics and checks closed bounds; independently, the translator reads whole-program classifications. Current diagnostics rerun; the translation-observable classification projection belongs in `G` ([rewriter:677–701](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModularTermination.cs#L677-L701)). |
| Test-runner generation | Replaces the resolved generated main body based on all selected test methods. Include the generated local body/view in the default module's `G`, or prove that a particular translation route cannot observe it ([RunAllTestsMainMethod.cs:96–241](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Rewriters/RunAllTestsMainMethod.cs#L96-L241)). |
| Auditor | Emits current report/diagnostic/file effects; rerun outside snapshots, without inventing a translation dependency for output it alone consumes ([Auditor.cs:120](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Auditor/Auditor.cs#L120)). |
| Replacement/duplicate names | Rerun current admission and replacement binding; include any selected replacement identities actually read by translation in its frozen view. |
| External plugin | Require a complete audited input/effect contract; otherwise bypass affected persistent stages. |

`FinalResolutionKey.FinalizeKey` in the patch frames `L` and canonical `G` bytes. It does **not** implement the global-view extraction or modify the translator's global-analysis access; those remain implementation requirements. Until they exist, the prototype is not sufficient to enable translation-cache hits under modular termination or any other unprojected global dependency.

### Persistent `ModuleResolutionResult` format

Use a typed, versioned graph format, never `BinaryFormatter`, reflection-deserialized CLR objects, a `.doo` printed AST or a previously mutated live result. The snapshot boundary is the return of `ResolveModuleDeclaration`, before program postpasses and translation mutate objects. Freeze/copy at publication.

| Section | Exact required contents |
|---|---|
| Fixed header | Magic `DAFNY-RESOLUTION`, major/minor schema, encoding/endianness, flags, bounded byte/section/node counts, local input digest L, full canonical local input bytes, payload digest, producer namespace/build/assembly identities, resolver engine, codec identity. Unsupported required fields/tags → miss. |
| Source table | Content digests/bytes or independently verified references; logical URI and current URI mapping; exact offsets, span lengths and nested/refinement/generated-origin chains; tokens/trivia needed by diagnostics, hover and linting. |
| Owned node graph | Deterministic node IDs, explicit union tags and fields for all parsed and elaborated declarations, statements, expressions, patterns, types, attributes and generated declarations reachable from this module result. Preserve sharing/cycles via typed references. |
| Resolved facts | Resolved names/members/constructors/type applications, solved type targets, characteristic/variance/ghostness facts, inheritance, datatypes, class/non-null associations, decreases/reads/requires/ensures, inferred contracts/triggers, call graphs/SCC facts, export/refinement/facade identity, cardinality/termination metadata needed by fresh global analyses. |
| Returned result | Root declaration handle; module-signature graph (including generated abstract signatures); ordered diagnostics; class-member tables. This is the serialized content of all four fields, not their CLR representation. |
| External reference table | `(logical module identity, selected export/facade view, declaration path/kind, generic arity/owner, expected target fingerprint)`; builtins use structured builtin keys. Original exact prerequisite keys are retained for initial binding. |
| Scope table | Stable scope/owner/view handles, opaque/revealed memberships and augmentation deltas. Raw `maxScopeId` counters and memoized visibility caches are excluded. |
| Effect journal | Builtin creations/requests, class-table additions outside the owned module, scope augmentation of imported declarations, generated declarations and all other mutations not contained in the returned graph; deterministic order and current-source diagnostic anchors. |
| Integrity/trust | Envelope digest and producer-authentication metadata as required by storage policy. A matching hash detects corruption; it does not establish that an attacker-supplied resolved type fact is true. |

Allocate shells, bind local references, then bind external handles against the current dependency instances and interned builtin catalogue. Validate kind/arity/owner, export visibility, source ranges, count bounds, graph closure and solved-proxy invariants before publishing anything. Preserve newtype/subset/opaque identities; do not normalize every type down to its base. Legacy reachable `TypeProxy.T` must be solved or represented by an audited inference result; refreshed `PreTypeProxy`/`TypeRefinementWrapper` state must be finalized, with printable pretypes and relevant declaration associations retained. An unexpected unresolved proxy is a miss, not a guessed type.

Restore a fresh `BatchErrorReporter` using current options and replay structured diagnostics in original order. Direct output side effects, such as `--print-exported-views`, must be captured as stage events or make that mode noncacheable; replaying only `AllMessages` misses them ([ModuleResolver.cs:652–666](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModuleResolver.cs#L652-L666)). Initial v1 caches only fully successful complete resolution; error/cancellation partial graphs recompute. Negative-result resolution caching can follow after invalid/partially populated graph handling is specified.

Preparation is detached and has no visible effects; an accepted snapshot commits through the same publication path as cold resolution. In particular, `Program.ModuleSigs`, `classMembers`, `moduleDeclarationPointers`, and diagnostic ordering retain one coordinator ([ProgramResolver.cs:102–119](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ProgramResolver.cs#L102-L119)). Aliases and abstract imports must bind to the newly loaded prerequisite declaration, not the parsed placeholder or yesterday's object. Never persist `CardinalityValidationReceipt`, cancellation state, translation naming counters, proof-dependency managers, reporter locks or visibility memo tables.

### What the C# patch supplies

See [content-resolution.patch](261/patches/content-resolution.patch).

`ResolutionInputs.Encode` supplies explicit canonical local-key framing and ordering; `CoversExactly` validates every current graph successor and its coordinator-supplied key, and `Freeze` copies mutable buffers. `FinalResolutionKey.FinalizeKey` supplies the separate R framing after G is computed. `ContentResolutionSession` computes local content keys only after prerequisites have completed; if any prerequisite lacks a valid content key, the dependent falls back to cold resolution. `CachingResolver` selects this separate session only when injected; the legacy cache and GUID behavior are unchanged by default. It also bypasses the existing `cache.Builtins` live-object reuse in content mode. A successful load requires a detached, fully rebound snapshot prepared by `IResolutionSnapshotCodec`, with a current cancellation/generation guard immediately before coordinator publication; nothing in the prototype claims that codec exists. This is a concrete integration seam, not a shipping persistent-resolution implementation.

### Resolution mutation inventory R1–R21

The numbering follows the earlier [F2 inventory](https://github.com/erniecohen/dafny/blob/7b05298628b170ec93cf393b01ed8e08d83106fe/docs/dev/design/241-front-end.md#32-inventory-of-shared-mutable-state-reached-by-module-resolution); these are snapshot/parallelism dispositions, not claims that the proposal implements all of them.

| Site | Required disposition |
|---|---|
| R1 `Program.ModuleSigs` | Reconstruct from typed result; publish at coordinator, never deserialize the old program dictionary. |
| R2 class-member tables | Serialize owned tables and builtin additions; publish complete read-only prerequisite tables before dependents. |
| R3 declaration-pointer setters | Rebind against current graph; publish parent slots before dependent parent starts. |
| R4 `Bitwidths` | Serialize structured builtin demand, not shared set history; emit deterministic projected order. |
| R5 builtin `SourceDecls` and arrays/arrows/tuples | Immutable structured-key interner; journal demands and rebind all references. |
| R6 `PreTypeBuiltins` and first-touch signature fill | Initialization must finish under interner ownership and depend only on the builtin key/options. |
| R7 max non-ghost tuple size/token | Recompute current canonical aggregate and token; not a stale cached global maximum. |
| R8 `MyHash` | Replace with exact canonical builtin-definition manifest for persistent identity. |
| R9 fresh visibility-scope IDs | Deserialize logical scope handles into fresh identities; never persist a process counter. |
| R10 lazy module visibility scope | Establish exactly one scope before parallel publication. |
| R11 imported visibility augmentation/memoization | Replay semantic augmentation as owned deltas; synchronize or eliminate shared mutation; discard memo caches. |
| R12 thread-local type scope state | Enter/leave explicitly in each synchronous worker with `finally`; never snapshot ambient scope stacks. |
| R13 refinement-wrapper debug count | Fresh nonsemantic allocation metadata; keyed/replayed only if a requested diagnostic actually exposes it. |
| R14 quantifier counter | Fresh internal identity; translation must use stable context-local naming wherever it is observable. |
| R15 reusable newtype walk set | Worker scratch, excluded from artifacts. |
| R16 diagnostics | Serialize structured events/origin handles; commit in deterministic order under current policy. |
| R17 module rewriters | Their elaborated local AST/effects enter the result; reviewed built-ins fingerprinted, unclassified external hooks ineligible. |
| R18 program pre/post rewriters | Always rerun; input contributions feed L, translation-visible output contributions feed G. |
| R19 cardinality/replacements/duplicate names | Current admission outside cached module success; selected replacement facts enter G only when translation reads them. |
| R20 escaping inference proxies | Reject unsupported unresolved proxies; preserve finalized type/pretype facts and nominal identity. |
| R21 refinement-base cloning | Full readable base is a prerequisite; clone/rebind against current completed base with fresh owned identities. |


## 5. Early cutoff and observable dependency views

Let `B(m)` be deterministic history-free translation of the admitted module input, `P(m,t)` the exact pruned ordered task input, and `E(t)` the complete deterministic task execution envelope. Let `K(t) = H(E(t))`. Let `Run(E)` denote the eligible deterministic solver result/events; report rendering is a separate deterministic function of result plus current reporting metadata.

Assume input hashes are collision-resistant, cache entries are trusted and integrity-checked, all schema/build identities match, the current program has passed admission, and replay is allowed for this execution class. If a dependency changes, changed local dependency keys force re-resolution of affected dependents; changed finalized keys, including changed post-analysis projections, force retranslation. If the regenerated envelope is unchanged, `E_new(t)=E_old(t)`, then `Run(E_new(t))=Run(E_old(t))`; the stored result is exactly the result the admitted cold task produces. Neither the changed upstream source nor its module resolution key belongs in `K(t)` once it has been eliminated from the actual task input. Therefore reuse is sound. This is an equality argument, not a claim that logically equivalent SMT inputs have identical RU, counterexamples or diagnostics.

Reporting has the analogous condition: old rendered text is reusable only if the current report envelope is identical. Otherwise rebind/re-render structured results under the current origins/options; if that re-rendering cannot be proved faithful, keep the proof hit but recompute reporting data or miss. An unchanged semantic VC alone does not license stale source locations.

### Later observable-dependency refinement

Replace each whole prerequisite key on an ordinary import by `V(target, selected-view, consumer-mode)`, a content hash of the dependency information the dependent resolver is permitted to read. This refines the local-key L layer; current post-analysis projection G and finalized R still apply. Use nominal identities and export visibility, not just pretty-printed type signatures. Maintain both semantic and presentation projections where necessary. Refiner/base and body-inspecting rewriter edges retain an appropriately stronger view, up to the full resolved base.

| Both resolver engines must observe | Why |
|---|---|
| Stable declaration, module and facade identities; exported name tables; opened-import ambiguity; default/extended export selection; `provides` versus `reveals`; parent trait/class relationships and substitutions | Resolution chooses declarations and admissible visibility, not merely types with the same spelling. |
| Fully instantiated type signatures; generic arity, bounds, variance, equality support, auto-init/nonempty/reference characteristics, ghostness, member kinds/overrides, constructor/destructor facts, nominal subset/newtype identity | Both inference algorithms read these facts, some inferred from definitions. |
| Formal default values, requires/ensures, reads/modifies/decreases, inferred contracts/decreases and callable/termination metadata, relevant attributes and generated members | These can change call resolution, proof generation or later admitted behavior without changing a name/type list. |
| Transparent function/constant bodies; revealed datatype/type definitions and constraints/witness facts where exposed; explicit `reveal`/auto-reveal dependency closure; bodies/specifications used by refinement | Opaque exported signatures alone are not the entire readable view. |
| Any generated properties influenced by excluded bodies (e.g. inferred export characteristics) | Exclusion is valid only after projecting the final observable result; source spelling is insufficient. |
| Source origins and diagnostic anchors that the dependent can report, or a proved complete origin-rebinding interface | Location-only edits must not yield stale related locations or diagnostic arguments. |
| Builtin identities/signatures and effective options of the exported view | First-touch initialization and library-specific options cannot change a nominal type silently. |

Legacy-specific projection: fully solved `TypeProxy` targets and resolver-created type/member tables, while preserving opaque/revealable type boundaries. Refreshed-specific projection: final pretype signatures, constraints/refinement facts and printable views consumed by later passes, `PreTypeBuiltins` associations and final type conversion; unresolved proxy identities and allocation counters are excluded, never serialized as semantics. Both must publish read-only completed signatures before dependents start.

The observational proof obligation is: for fixed own input, equality of every declared view implies equal module resolution result modulo fresh identities and equal emitted event/effect journals. Enforce the boundary through a view API or traced reads and validate by differential tests over both resolvers. Any unclassified read upgrades the edge to the full dependency key. Rebinding external handles after a view hit uses stable logical declaration/view identity plus the expected observable fingerprint, not the old full dependency key; otherwise changing a hidden body would make the promised view hit unloadable. This also ensures a new declaration with the same printed signature but a different nominal owner is not mistaken for the old type.

Consequently, a method-body edit or opaque proof edit can avoid dependent resolution only when it leaves all observed resolved properties unchanged. An edit that changes inferred requires/decreases, revealed definitions, export visibility, a refinement base body or body-derived public characteristics legitimately invalidates dependents. Whole-program cardinality/modular-termination admission still reruns even when every relevant imported view matches.


## 6. Storage and publication

All hashes are SHA-256 with a domain tag, schema number, length-framed fields,
and canonical integer/string encodings; sequences retain semantic order. Hashes
identify bytes, not authenticity. V1 accepts only local, access-controlled caches
written by a trusted pinned producer. A malicious writer able to replace both an
answer and its checksum can forge `Valid`; sharing with untrusted producers needs
an authenticated producer policy or proof checking and is outside V1.

| Object | Layout and contents |
|---|---|
| Immutable bytes | `v1/objects/<kind>/<digest[0:2]>/<digest>`; kind is domain-separated and digest covers the complete uncompressed canonical payload. |
| Local resolution candidates | `v1/local-resolution/<L>` → detached pre-global typed module snapshot; auxiliary phase cache, not global admission or authority for translation. |
| Resolution table | `v1/resolution/<R>` → local snapshot plus current post-analysis projection digest, input-manifest digest, typed-module object, diagnostics object, source/rebinding map, producer/profile. |
| Translation table | `v1/translation/<R>/<profile>` → hash of exact module BPL bytes plus materialization/source/proof-map sidecars; fold profile into the resolution options identity if maintaining a literal one-key table. |
| Task table | A logical union of `v1/task-inputs/<K>` (Boogie/command-plan/sidecars still to check) and `v1/task-results/<K>` (complete eligible result); separate immutable namespaces let a pending task become completed without overwriting immutable bytes. |
| Rendering data | Result-independent current source/obligation maps; optional rendered report objects keyed separately by presentation inputs. |
| Coordination | Advisory leases and temporary files live outside immutable objects; neither their presence nor age authorizes a hit. |
| Access journal | Best-effort, bounded append-only events for eviction and explicit profiling; no per-hit mutation of trusted artifact bytes. |

Publication protocol:

1. Create a randomly named temporary file in the destination filesystem with exclusive creation and private permissions.
2. Write a versioned envelope, payload length, input key, output digest and payload; bound lengths before allocation, verify the finished file, then flush it.
3. Publish immutable object bytes using an atomic no-replace rename/link; if another producer won, validate and reuse equal bytes.
4. Publish the referencing table entry only after all referenced objects are durable; flush containing directories where required by the platform durability contract.
5. If the same full input descriptor has unequal **canonical deterministic result** bytes under the same profile, quarantine/disable the key and recompute; do not silently select the last writer. Producer timestamps, historic timings and access/creation metadata live in separate observation records and do not participate in this comparison.

The generation files and atomic index update are separate: an unreferenced object
is harmless; a half-written index is rejected. An interrupted producer leaves a
miss, not an answer. In a non-determinism dispute, an already emitted prior result
cannot be undone by quarantine; the correctness argument still requires the
deterministic-producer precondition. Detecting a dispute is an additional safeguard,
not a replacement for that precondition.

| Event | Required response |
|---|---|
| Missing object, digest mismatch, unknown schema, truncated/trailing payload, unknown enum/node tag, malformed reference | Miss and recompute; keep ordinary program diagnostics independent of cache-health diagnostics. |
| Wrong pin/options/solver/profile, codec unsupported, failed source rebinding | Miss; never coerce an old format into the current one. |
| Permission/I/O failure | Disable affected reads/writes for the invocation; run the uncached pipeline. |
| Cancelled producer | Publish no result; an independently completed immutable object may be retained, but never publish stale IDE diagnostics. |
| Two processes compute the same key | Either may finish; publication is idempotent; leases suppress duplicate work only as a performance hint. |
| Different semantic outcomes for the same full descriptor | Poison the key/profile, record a requested diagnostic, and run fresh; treat as a determinism/key defect requiring investigation. |
| Cache hit races with eviction | Hold an open file/private immutable byte buffer or miss; never continue with a partially read graph. |
| Read-only cache | Read verified entries; compute misses normally without attempting publication. |

Eviction: size-bounded LRU approximation over table roots; mark referenced immutable
objects, remove unreachable objects after a grace interval, and tolerate missing
references as misses. Producers retain leases on objects until index publication;
collectors exclude active leases or use generations. Startup removes abandoned
temporary files only after a conservative stale interval. No eviction changes a
logical result. Unsupported filesystems fail closed to ordinary execution rather
than emulate atomic publication with overwrite-in-place.

[model_storage.py](261/models/model_storage.py) exercises actual filesystem no-clobber publication,
separate processes, conflicts, corrupt/truncated/version-mismatched entries,
abandoned temporary files, and eviction. It is a POSIX executable model; a product
implementation needs corresponding Windows/macOS publication tests.

## 7. CLI and IDE use one service

Add a core `IContentAddressedCache` with resolution, translation and task-store
interfaces, injected into both front ends. The CLI's current
`EmptyVerificationResultCache` and the IDE's `VerificationResultCache` remain on
the option-off route. Do not extend Boogie's legacy checksum cache to stand for
this service. [CLI][cli], [task][b-task], [LS composition][ls-composition]

| Integration point | Opt-in behavior |
|---|---|
| `Compilation.ParseAsync` / document loader | Snapshot all open-document overlays, include/project/library bytes and logical URIs once; file mtime is never content identity. |
| `ProgramResolver` / `CachingResolver` | Share module-key construction and typed materialization; retain current graph checks and whole-program admission on hits. |
| `DafnyProgramVerifier.GetVerificationTasksAsync` | Lookup the resolution-key translation row; on hit reconstruct a private BPL graph and current sidecars; on miss use the history-free translator and publish the same format. |
| Boogie modern task boundary | Construct/validate the exact execution descriptor and use the same result store as the CLI. |
| `Compilation.VerifyTask` | A hit enters the existing `Completed` event path after `CanVerifyPartsIdentified`; no direct gutter/report shortcut. |
| `IdeStateObserver.OnNext` | Existing version gate still rejects old compilation versions; also check the cancellation/generation token before publication. |
| Verification from an explicit seed | `FromSeed` produces a new exact task key; a prior unseeded completion cannot substitute. |
| Repeated filtered requests | Keep the currently requested source snapshot/prelude lifetime and complete task selection; explicit fresh-verification/batch semantics bypass reuse where that interface promises fresh checks. |
| Unsaved edit / move / revert | Overlay content changes R; report locations bind to the current overlay; reverting may reuse old immutable content without restoring old live object references. |

Do not key by IDE document version or compilation GUID: they control publication,
not semantic identity. CLI and IDE with the same snapshots, options, toolchain and
deterministic execution profile produce the same task keys. Different selection
provenance or effective options can legitimately differ. Interactive progress,
timestamped telemetry and scheduling-event timing are not reproducible final
reports; if an interface exposes them as exact required output, it is ineligible
for the exact replay contract. [Compilation][compilation], [IDE observer][ide-observer]

Ownership: each compilation loads private mutable nodes or certified immutable
dependency views; never return the earlier compilation's `ModuleResolutionResult`,
tokens, reporter, resolver, or `ICanVerify` references. Keep proof maps alive through
all completion/report consumers, then release them independently of the translation
cache. Budget bytes for loaded modules, solver contexts, queued work and completed
results; cache pruning is not proof that those graphs have been released.

## 8. One-PR milestones and acceptance tests

These PRs preserve the existing default route and add no axioms or assumptions.
Each product PR supplies the fork's release-note, appropriate unit/Lit regressions,
manual/help changes for its options, `.github/review/base` bookkeeping, and the
suite/Std option-off verdict and RU comparisons. No expected verdict is updated
to make an optimization pass. The study/proposal PR is documentation and models;
it does not alter product files. [Fork rules][agents], [release gates][review]

| PR | Deliverable | Required acceptance evidence |
|---|---|---|
| P1 | Complete task descriptor + option inventory + eligibility, with a recording transport; no hits yet | Serialized descriptor equality implies identical complete command plans and response policies; mutate each input family; unknown options/plugins reject caching. Include whole-program `UsedTypes`, per-split seeds and all limits. |
| P2 | Versioned immutable store + trusted namespace + fault handling | Multi-process equal/conflicting publishers, interrupted writes, malformed lengths/ids, missing blobs, stale leases, read-only store, eviction races; no false hit. |
| P3 | Late task-result reuse for completed `Valid` checks | Two fresh processes on an all-eligible successful fixture: second has **zero proof-solver process starts, zero check calls**, identical task manifest/verdict/RU/final deterministic output. Instrument below the transport. Report identity/version probes separately; with persisted version metadata under the verified executable/dependency/environment fingerprint, the fully warm test must also have zero version probes. |
| P4 | Exact negative/OOR records and reporting sidecars | Errors with nested related locations, several counterexamples, models, unsat cores, proof coverage, seeds and warning-to-error behavior; raw `unknown` is not accepted merely because a current enum says `Invalid`. Byte-compare deterministic final text. Current timing-bearing JSON/CSV remain ineligible; comparing deterministic JSON/CSV requires a separately specified logger contract, not an assumption in this PR. |
| P5 | History-free module translation ownership/naming | Actual Dafny translation alone, repeated, reversed order and with unrelated modules; compare exact BPL bytes, source/proof maps and canonical task descriptors. Run both resolvers, Std, trait functions, captured variables, matches and reveals. |
| P6 | BPL translation persistence + common materializer | Zero translator calls on an unchanged module after process restart; direct/miss/hit routes agree on graph semantics, task manifest, report mapping and command plans, including literal aliasing and definition-axiom ownership. |
| P7 | Content module keys + frozen resolver input/session | No `CloneId` in opt-in semantic keys; one-file multi-module edits, import aliases/ambiguity, nested/prefix modules, refinement/facades/exports, builtins and all rewriters. Independent cold resolution oracle across both resolvers; distinguish local L from finalized R and exercise downstream modular-termination changes to G. |
| P8 | Typed module codec, rebind/validate, persistent hits | Unchanged process-restart module uses zero body-resolution calls; no stale graph pointers; global duplicate/replaceable/cardinality checks still run. Unsupported nodes/proxies/schema/plugins miss, not partially load. |
| P9 | Parallel DAG resolution + immutable prerequisite views | Parallel completion orders produce identical module artifacts and diagnostics; no worker mutates imported graphs/builtins/scopes; cancellation and dependency errors prevent publication; compare 1 and multiple workers. |
| P10 | Shared IDE adapters and lifecycle | Restart LS then warm reuse, unsaved overlays, rapid cancellation/edit/revert, explicit seed, cross-project roots, CLI-to-IDE reuse, no old-version diagnostics; existing cache lifecycle/cardinality tests remain valid. |
| P11 | Observable export-view fingerprints | Private method/proof edit avoids dependent resolution; transparent/revealed/specification/inference/negative-lookup edits invalidate it; verify materialized views match full cold behavior for both resolvers. |

P1/P2 can proceed independently. P3 depends on P1/P2; P4 follows P3. P5 and P7
can proceed independently after agreeing on frozen inputs; P6 requires P5, P8
requires P7, P9 requires isolated ownership rather than just a cache hit, and P10
joins the implemented stages. P11 deliberately follows the conservative full-key
design. Every milestone is one reviewable PR; a failing acceptance gate keeps its
option disabled or its unsupported case on the old route.

### Required edit tests, with precise oracles

1. **Unchanged:** record the ordered full task descriptor bytes on a cold run;
   restart the process against the same store; require identical task multiplicity
   and output with zero solver calls for every eligible task. Count tasks and
   calls separately: identical tasks may share an entry.
2. **One function:** edit one body; independently regenerate every current task
   descriptor with cache reads disabled; solver execution on the cached run must
   equal the set of eligible new distinct descriptors absent from the old store,
   plus every ineligible task. Do not assume syntactically dependent functions are
   exactly the changed tasks: pruning, inlining, `UsedTypes` and split plans decide.
3. **Early cutoff:** edit an imported opaque method body without shifting the
   tested dependent's source locations; R changes transitively and dependent
   resolution/translation run again; equal regenerated task descriptors reuse
   their results. Repeat with source relocation to test current-location rebinding.
4. **Global rejection:** warm all module/task records; add a downstream cardinality
   cycle or duplicate compiled name; reject the new program before any cache hit
   can become a reported verification result.
5. **No hidden history:** arrange hits before a miss; its result/RU/command plan
   must match the same miss in a fresh uncached process. Reused-history mode is
   rejected, not accepted based only on the hit's own key.
6. **Elapsed time:** prior success under a nonzero wall deadline is not reusable;
   current timestamp/duration log modes remain unsupported for literal byte
   equality. This is an expected eligibility result, not a failed cache lookup.
7. **Global translation facts:** add a downstream trait implementation under
   modular termination; L of its upstream module may stay fixed, but the current
   OpenTraits/ReachesOpenDispatch projection and hence R must change wherever
   generated checks change. Add an unrelated implementation whose projection is
   unchanged and require the corresponding R/BPL hit to remain possible.

`DafnyOptions.GetZ3Version` currently launches the executable to inspect its version;
persist that result only under verified executable, dependency and relevant
environment bytes. A path-based process-local memo is not the cross-process
identity cache. [Version probe][version-probe]

## 9. Next question answered: can BPL text stand for the live translated graph?

**Not automatically.** The fixed translation table is useful, but text equality
alone does not establish equality of the original in-memory BPL graph: object
sharing, literal identity, source origins, definition-axiom ownership and weak-table
preparation state can be lost. Nor does a saved late task hash prove that a
skipped translation/preparation stage had the right inputs. [Earlier exact-cache
analysis][pipeline], [Boogie preparation][b-engine], [Boogie review][b-review]

Keep the owner's BPL text object and resolution-key lookup; add a versioned
materialization sidecar. There are two implementable routes:

| Route | Required invariant |
|---|---|
| Faithful graph materializer | Typed/tagged nodes, sharing/alias IDs, symbol bindings, definition-axiom ownership and source/proof maps reconstruct the same graph observable by Boogie. A bare parse of BPL does not qualify. |
| Common canonical materializer | With the cache option on, **both a cold miss and a warm hit** go through the same BPL-plus-sidecar parser/materializer before preparation. The cache-enabled cold reference then has the same graph construction. Assess any behavior/RU changes against legacy separately; never silently activate `/structuralLiterals` to paper over them. |

The first route aims to preserve the current graph semantics; the second is a
possible option-on specification, not evidence of legacy equivalence. Until one
passes P6's oracle, store the text for diagnostics/inspection and continue fresh
translation/preparation. No row authorizes reusing an unchecked graph.

The later prepared-task cache has the same proof obligation. For
`prepare(input, state) = (artifact, state', diagnostics)`, either make all state
private and deterministic or capture/replay its entire observable delta. Load at
a post-preparation API; feeding a passified graph into today's preparation pipeline
can transform it twice. This is the first follow-on implementation question after
the design below, not a prerequisite for late solver-result savings.


## Validation supplied with this study

The four standard-library Python models compare cached computations with fresh
oracles and include negative controls. They validate the meaning of the proposed
keys and ownership rules, not Dafny's source implementation or Z3 determinism.
Run all with `python3 261/models/run_models.py` from this document's directory,
or run each model directly. The exact executed totals are in
[VALIDATION.md](261/VALIDATION.md).

| Model | Coverage |
|---|---|
| Task keys | 128 cold/warm configurations, complete field mutations, UsedTypes omission, order-sensitive RU, adaptive errors, source rebinding, early cutoff, process-history coupling, timed-success exclusion and elapsed-time counterexample. |
| Module DAG | Independent cold interpreter and 400 seeded edit scenarios, exact hit/miss oracle, zero warm mock calls, one-function edit, early cutoff, observable views, corruption, cycles, fresh admission and post-analysis key finalization. |
| Naming | 96 permutation/module comparisons and 128 concurrent translations, legacy failure control, ignored stale AST memos, nested scopes/prefixes, proof-label injection, module report isolation and local match-case state. |
| Storage | 20 tests including actual concurrent producer processes, atomic equal/conflicting publication, corruption/truncation/domain/version/length checks, abandoned temporary files and eviction. |

Both C# patches were checked with `git apply --check` against the requested pin;
this checks applicability, not compilation or semantic correctness. There is no
installed `dotnet` executable in the execution environment. No product defaults,
release baseline, expected verdicts or resource-count files are changed by this
documentation/proposal PR.

**Prototype limits:** record fields typed `byte[]` or `IReadOnlyList` do not by
themselves enforce immutability; freeze/defensively copy before hashing and use.
The input-capture visitor, total runtime option registry, global projection,
complete typed codec, stable report carrier, storage adapters and CLI/IDE wiring
must be implemented before either proposal is enabled. Unsupported/error cases
must take cold paths; ordinary source errors and cancellation must retain their
existing behavior. The source option audit is deliberately conservative, with
761 occurrences, and is not claimed to enumerate every runtime value.

## Settled enough to implement, and what this frees

- The late execution-program key and separate current report binding free P1–P4: input recording, immutable storage, qualified successful-result reuse, then exact negative/OOR evidence.
- Local `L` plus current analysis projection `G` and finalized `R` free module snapshot and translation ownership work without making downstream global facts into cyclic import edges.
- The typed codec/rebinding boundary, fresh admission barrier and per-module proof maps settle the soundness boundary; workers can implement interfaces without treating cached AST identity or an old validation receipt as proof.
- The one-function and early-cutoff tests have independent descriptor/input oracles; cache-hit counts and faster wall time alone are not acceptance evidence.

## What is expected to change

- The exact option classifications will shrink conservatively keyed inputs as instrumented reads establish stage boundaries; unknown inputs continue to disable reuse.
- The supported solver profiles and negative-result classes will expand after their deterministic behavior and complete replay are established.
- The typed AST/Boogie materialization schema and `G(m)` projection will change as field/alias/read audits expose the remaining requirements; version every such change.
- Current timing-bearing report equality needs an explicit contract decision; this design does not manufacture a current elapsed time from historical observations.

## Next question

Which exact node aliasing, definition-axiom ownership and model/source sidecars
must the first faithful Boogie materializer preserve? Section 9 gives the
recommended proof boundary and a differential test; implementing that materializer
is the next investigation after the late cache starts, while the typed module
codec proceeds independently.

## Feedback on the request

The pins, fixed architecture, early-cutoff requirement and executable-model
deliverables make the review actionable. The essential correction is to separate
stable verification observations from current elapsed-time observations, and to
distinguish pre-global local resolution from the finalized translation context.
Keeping those distinctions explicit is necessary to meet soundness before
optimizing hit completeness.

Stopped because the requested design/review/model list is finished at the stated
prototype depth. First implementation item not done: the complete typed snapshot
codec and global-view capture; the C# build and real Dafny/Boogie differential
integration tests have not been run.

[request]: https://github.com/erniecohen/dafny/issues/261#issuecomment-6805467985
[agents]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/AGENTS.md
[review]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/REVIEW.md
[pipeline]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/docs/dev/design/241-pipeline.md
[frontend]: https://github.com/erniecohen/dafny/blob/7b05298628b170ec93cf393b01ed8e08d83106fe/docs/dev/design/241-front-end.md
[json]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyDriver/JsonVerificationLogger.cs#L50
[csv]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyDriver/CSVTestLogger.cs#L25
[mod-analysis]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModularTermination.cs#L145
[mod-read]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs#L150
[used-types]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/UsedTypes.cs#L25
[checker]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCGeneration/Checker.cs#L129
[interactive]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/Provers/SMTLib/SMTLibInteractiveTheoremProver.cs#L194
[boogie-options]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Options/BoogieOptionBag.cs#L54
[caching-resolver]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyLanguageServer/Language/CachingResolver.cs#L84
[module-result]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModuleResolver.cs#L19
[builtins]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/SystemModuleManager.cs#L35
[declaration]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/TypeDeclarations/Declaration.cs#L173
[match]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/Statements/MatchVerifier.cs#L87
[proof-manager]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/ProofDependencyManager.cs#L17
[b-cache]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/ExecutionEngine/Caching/VerificationResultCache.cs#L22
[cli]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyDriver/CliCompilation.cs#L64
[b-task]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/ExecutionEngine/VerificationTask.cs#L30
[ls-composition]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyLanguageServer/Language/LanguageServerExtensions.cs#L30
[compilation]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Pipeline/Compilation.cs#L394
[ide-observer]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyLanguageServer/Workspace/IdeStateObserver.cs#L51
[b-engine]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/ExecutionEngine/ExecutionEngine.cs#L864
[b-review]: https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/REVIEW.md#structural-literals-structuralliterals
[version-probe]: https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/DafnyOptions.cs#L1240
