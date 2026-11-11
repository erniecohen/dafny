# Front end and Boogie generation for verification throughput: an implementation design

Issue: [erniecohen/dafny#241](https://github.com/erniecohen/dafny/issues/241).
Study this builds on: `docs/dev/design/241-pipeline.md`
([pull request 242](https://github.com/erniecohen/dafny/pull/242)), sections 6.2–6.7, 7 and 9.
That study names the hazards; this document goes one level down. It says what each of four
features takes to build, item by item, so that implementation workers can start from it.

This is documentation only. It proposes no change to defaults and contains no product code.

| | Feature | Study milestone | Proposed option (all default off) |
|---|---|---|---|
| F1 | Parallel Boogie generation and preparation | D7 | `--translation-workers N` |
| F2 | Parallel module resolution, both resolvers | D8 | `--resolver-workers N` |
| F3a | In-process resolution reuse in the CLI | D4 | `--resolution-cache-content` (study §6.3) |
| F3b | Persistent typed library snapshots | D6 | `--library-resolution-cache DIR` |
| F4 | Early release of per-module state; one resolved program for in-process parallel verification | new | `--release-verified-modules` |

## 0. Conventions

**Pinned sources.** Dafny paths are on `review/4.11.0` at commit `10dbccf`. Boogie paths are at tag
[`v3.5.5+review.a7d160a7`](https://github.com/erniecohen/boogie/tree/v3.5.5%2Breview.a7d160a7)
of erniecohen/boogie (commit `7873c818`). That tag is the package that `Scripts/fetch-boogie-packages.sh`
fetches and `Source/DafnyCore/DafnyCore.csproj:41` references. It is written `B:` below, so
`B:Source/Core/AST/Absy.cs:56` is that file at that tag.

**Equivalence target.** Each feature must satisfy two conditions:

- **Off:** the output is byte-identical to the build without the feature (fork rule, `AGENTS.md`).
- **On:** the printed Boogie program of every module (`--bprint`), the SMT input of every task, the
  proof-dependency ids and the diagnostics are byte-identical to the serial run. The check uses
  `--boogie /deterministicLiteralHashes:1 --boogie /restartProver`, the same inputs and the same options.

"Exact" below always means this condition.

**Canonical order.** In the CLI, modules are translated in the order of
`Source/DafnyDriver/CliCompilation.cs:297-298` (group by `ContainingModule`, `OrderBy(Origin.pos)`,
stable). The module window uses the same order at `CliCompilation.cs:396-399`. Translation `k` is the
k-th module in this order. For resolution, the canonical order is `dependencies.TopologicallySortedComponents()`
(`Source/DafnyCore/Resolver/ProgramResolver.cs:53`).

**Symbols.** All quantities are per process. The measurement worker supplies them; this document
supplies no numbers for them.

| Symbol | Meaning | Where the measurement worker gets it |
|---|---|---|
| `M` | verifiable modules | count of `translate` spans |
| `τ_m` | translation of module m (`DoTranslation`), including the prelude parse `p_m` | `PipelineProfile` spans `translate.prelude` and `translate` (`Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs:62-79`) |
| `π_m` | Boogie preparation (`GetVerificationTasks`) | span `prepare` (`DafnyProgramVerifier.cs:93-101`) |
| `ω_m` | wait for the translation mutex | span `translate.wait` (`DafnyProgramVerifier.cs:43`) |
| `σ_m` | summed solver time of m's tasks | task spans |
| `C` | `--cores` | — |
| `K` | `--verification-module-window` | — |
| `r_m` | resolution of module m (`ResolveModuleDeclaration`) | per-module resolver span |
| `R_g` | serial resolution phases: graph, heights, system module, program-level rewriters, admission, cardinality | resolver phase spans |
| `L_R` | critical path of the import DAG, weighted by `r_m` | DAG plus `r_m` |
| `W_R` | `Σ r_m` | — |
| `P`, `P_lib` | parse time: total, and the part that comes from `.doo` and library sources | parse spans |
| `A_res` | retained bytes of the resolved program | heap snapshot after resolution |
| `A_clone` | retained bytes of the parsed clone `programAfterParsing` | heap snapshot |
| `B_m`, `T_m` | retained bytes of m's Boogie program and of its prepared tasks | heap deltas around translate and prepare |
| `δ_lib` | `.doo` share of the front end: `(P_lib + Σ_{m∈lib} r_m) / (P + R_g + W_R)` | from the above |

## 1. What the probe found, and why it shapes F1

A design for exact parallel translation stands or falls on the following question. Does module k's
Boogie program depend on which translations ran before it in the same process? Reading the code
says yes, through at least four channels:

1. **Memoized variable names on the shared AST.** `AssignUniqueName` stores the first name it computes
   (`Source/DafnyCore/AST/Expressions/Variables/NonglobalVariable.cs:35-40`,
   `Source/DafnyCore/AST/Statements/Assignment/LocalVariable.cs:61-67`). It draws that name from the
   generator of the declaration being translated (`Source/DafnyCore/AST/TypeDeclarations/Declaration.cs:173`),
   and that generator is itself shared AST state. Every translation translates the visible declarations of
   **all** modules (`Source/DafnyCore/Verifier/BoogieGenerator.cs:837-866`, with `CurrentDeclaration = member`
   at `BoogieGenerator.Methods.cs:45`). So the first translation in the process that touches an
   imported declaration fixes that declaration's names for every later one.
2. **Unmemoized counters on shared generators.** `FunctionHandle` emits the handle axioms of f once per
   translation (`BoogieGenerator.Functions.cs:597-604`, with a translator-local `functionHandles`), and
   names their bound variables from `f.IdGenerator.NestedFreshIdGenerator("$fh$")`
   (`BoogieGenerator.Functions.cs:662`). That call advances f's shared counter. In module k the prefix is
   `$fh$c`, where c is the number of earlier translations in the process that used f as a value.
3. **Proof-dependency ids.** These come from one counter per manager (`Source/DafnyCore/Verifier/ProofDependencyManager.cs:19,31-40`),
   and the manager is shared by every module of the request (`CliCompilation.cs:276-278`). Module k's
   ids therefore start at the total count of modules 1..k-1. The current definition
   (`ProofDependencyManager.cs:28-29,42-45`) also carries over from one translation to the next.
4. **Per-translator caches stored on the AST.** These are `LetExpr.translationDesugaring`, keyed by the
   last generator that used it (`Source/DafnyCore/AST/Expressions/Variables/LetExpr.cs:24-35`), and
   `Label.uniqueId`, which takes its id from whichever generator first asks
   (`Source/DafnyCore/AST/Statements/ControlFlow/Label.cs:8-15`). The second one may be the translator-local
   `defaultIdGenerator` (`BoogieGenerator.cs:2018-2028`).

`BoogieGenerator.Reset` (`BoogieGenerator.cs:1686-1693`) does not undo any of this. It clears
`codeContext` before calling `CurrentIdGenerator.Reset()`, so the call resets only the translator-local
`defaultIdGenerator`.

The probe workflow `issue241-front-end-probe.yml` on branch `scratch/issue-241-front-end-probe` measures
these channels. It runs a three-module program, and eight standard-library files each verified in
isolation against the full run. It prints every module's Boogie program twice: once in the full run,
and once with only that file selected (`--filter-position`). It counts the lines that differ raw, after
proof-dependency ids are renumbered, and after `$fh$` prefixes are renumbered as well. The results are
in §10. They give the size of the problem in three parts:

- the channels that a counter offset fixes;
- the channels that need replaying the names;
- whatever residue remains.

**Consequence for every cache.** "The Boogie program of module m" is a function of m **and the set and
order of modules translated before it in the process**. A key for a cached translation or a prepared
artifact (study B3, B4, D2, D3) must therefore include that history. The alternative is to make
translation history-free first, which F1's machinery provides as a by-product (§2.3, mode H).

---

## 2. F1 — Parallel Boogie generation (D7)

### 2.1 What is serialized today

`DafnyProgramVerifier.GetVerificationTasksAsync`
(`Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs:29-106`) holds one `SemaphoreSlim`
(`:23`, acquired `:41`, released `:104`) around three steps:

- `Type.ResetScopes()` (`:54`), a new `BoogieGenerator` (`:61`, which parses the prelude) and
  `DoTranslation` (`:71`), all on `DafnyMain.LargeStackFactory`;
- optional `--bprint` (`:86-90`);
- `engine.GetVerificationTasks` (`:93`), which is Boogie resolution, type checking, dead-variable
  elimination, modifies collection, coalescing, inlining, preprocessing, passification and splitting
  (`B:Source/ExecutionEngine/ExecutionEngine.cs:864-905`).

With `--verification-module-window K` the CLI admits up to K modules, but the semaphore still runs one
translation and preparation at a time. Today's serial service time per module is
`ω_m + τ_m + π_m`. `Compilation.VerifyUnverifiedSymbol` takes a ticket before translation and awaits
it before task submission (`Source/DafnyCore/Pipeline/Compilation.cs:397,435,461`). That ordering of task
submission is independent of the mutex, and F1 keeps it.

### 2.2 Inventory of shared mutable state reached by translation

Rows T1–T22 are Dafny. Rows B1–B12 are Boogie preparation. The *Change* column uses the vocabulary
of the brief: privatize, preassign in serial order, relocate after the fact, freeze, intern, keep serial.

| # | Item (path:line) | Written by, when | Change | How serial order is recovered |
|---|---|---|---|---|
| T1 | `NonglobalVariable.uniqueName` (`AST/Expressions/Variables/NonglobalVariable.cs:35-40`), `LocalVariable.uniqueName` (`AST/Statements/Assignment/LocalVariable.cs:61-67`) | First `AssignUniqueName` in any translation (40+ call sites, e.g. `BoogieGenerator.cs:746,1730,1885,3174-3195,3889-3938`, `ExpressionTranslator.cs:1972-2017`, `Functions.cs:159,412,419,950`, `DataTypes.cs:824-825`, `Methods.cs:757`) | **Journal + ordered replay** (§2.3, class E1) | Replay the journal in canonical order against the committed store and compare. Re-translate on mismatch |
| T2 | `Declaration.IdGenerator` base-level prefix counts (`AST/TypeDeclarations/Declaration.cs:173`; `Verifier/FreshIdGenerator.cs:10,50-62`) | `AssignUniqueName` (T1) and `FreshId` with `CurrentIdGenerator` of a `codeContext` declaration (`BoogieGenerator.cs:2020-2028`) | **Journal + ordered replay** (E1 for memoized names, E2 for bare counters) | The replay recomputes every value from the committed counts |
| T3 | `Declaration.IdGenerator.Tip` and the stack levels pushed (`FreshIdGenerator.cs:10-27`) | `Push`/`Pop` in statement translation (`MatchVerifier.cs:56,80`, `TrStatement.cs:406-408,614-692,746-762`, `TrLoop.cs:131-162`, `IfStatementVerifier.cs:29-48`, `TrForallStmt.cs:17,70`, `BlockByProofStmtVerifier.cs:11-13`), only while `codeContext` is that declaration, so only in the owner module's translation (`BoogieGenerator.cs:176`) | **Privatize**: each translation gets its own overlay of every generator it touches, with `Tip` empty. Counts at depth > 0 come from the fresh dictionary that `Push` makes (`FreshIdGenerator.cs:18`) and never reach shared state | None needed: serial `Tip` is empty between translations because pushes and pops are balanced. Assert it when committing |
| T4 | `f.IdGenerator` advanced by `NestedFreshIdGenerator("$fh$")` (`Functions.cs:662`) | First handle use of f in each translation | **Relocate after the fact** (E2): the overlay returns a placeholder prefix `\u0001k.n\u0001` and records the node that holds it | At commit, the replay computes `$fh$c` and patches the recorded `Bpl.TypedIdent`/`IdentifierExpr` names (substring-exact, because U+0001 cannot occur otherwise) |
| T5 | `Label.uniqueId` (`AST/Statements/ControlFlow/Label.cs:8-15`) | `AssignUniqueId(CurrentIdGenerator)` (`ExpressionTranslator.cs:179,1443`, `LetExpr.cs:382`) | E1 when the generator is a declaration's. When it is the translator-local `defaultIdGenerator`, the memo is still shared, so it is E1 as well | As T1 |
| T6 | `LetExpr.translationDesugaring`, `lastBoogieGeneratorUsed` (`AST/Expressions/Variables/LetExpr.cs:24-35`) | `LetDesugaring` (`BoogieGenerator.LetExpr.cs:132-200`) | **Privatize** into a translator-local `Dictionary<LetExpr, Expression>`. Today a race is fatal: A writes, B writes, A reads null and adds `e` to `letSuchThatExprInfo` a second time, which throws (`BoogieGenerator.LetExpr.cs:182`) | Already per translator in serial (keyed by generator) |
| T7 | `AssertLabel.E` (`AST/Statements/Verification/AssertLabel.cs:8`) | `Methods.cs:1895`, `Iterators.cs:92`, `TrPredicateStatement.cs:120`, `Functions.Wellformedness.cs:130`, all in the owner's translation | **Privatize** into a translator-local map: a `Bpl.Expr` belongs to one program and must never be shared | Owner-only in serial |
| T8 | `ProofDependencyManager.proofDependencyIdCount`, `ProofDependenciesById`, `idsByMemberName` (`Verifier/ProofDependencyManager.cs:17-19`) | `AddProofDependencyId` (`:58-63`) from 20 call sites (`BoogieGenerator.cs:3640-3711`, `Functions.cs:571`, `Obligations.cs:156,259`, `TrCall.cs:376,418`, `TrLoop.cs:264`, `TrAssignment.cs:245-560`) | **Relocate after the fact** (E3): a translation-local manager issues `\u0001n\u0001` and records the `QKeyValue` it created (`:61-62`, the only place ids are made) | Commit sets `Params[0] = "id" + (base_k + n)`, with `base_k` the committed count. Then it merges the local maps into the global ones in local insertion order, which gives the same `Dictionary` insertion order the coverage report relies on (`:21-26`) |
| T9 | `ProofDependencyManager.currentDeclarationString/currentDeclaration` (`:28-29,42-45`) | `SetCurrentDefinition` (`Functions.cs:570`, `Fields.cs:174`, `DataTypes.cs:803`, `Methods.cs:557,881,979,1798`) | Privatize. The local manager starts at a sentinel *inherited* | At commit the sentinel bucket goes to the committed current definition, which is the last one set by translation k-1. That reproduces today's carry-over exactly |
| T10 | `VisibilityScope.cached` (`AST/VisibilityScope.cs:28,31-50`) | Every `VisibleInScope` (via `Declaration.IsVisibleInScope`, `Declaration.cs:109-111`, from `BoogieGenerator.cs:180-195`). A read mutates the `Dictionary` of every visible declaration | **Lock** per scope (or `ConcurrentDictionary`). The result is a pure memo, so order is irrelevant | No order effect |
| T11 | `Type._scopes`, `scopesEnabled` (`AST/Types/Types.cs:36-40`) | `Type.EnableScopes/PushScope` (`BoogieGenerator.cs:769-771,891-892`) | Already thread-local. Each translation runs synchronously on its own `LargeStackFactory` thread. Keep `Type.ResetScopes()` first, and pop in `finally` | — |
| T12 | `FuelSetting.SavedContexts` (`BoogieGenerator.cs:4529,4790-4796`) | Push/pop of fuel contexts | Privatize into a translator field. Today an `AsyncLocal` reference would be shared if an ancestor context ever set it | — |
| T13 | `SystemModuleManager.Bitwidths`, `SystemModule.TopLevelDecls`, arrow/tuple/array tables (`AST/SystemModuleManager.cs:15-19,72`) | Not written by translation: `TupleType(..., false)` only looks up (`BoogieGenerator.cs:2518`, `SystemModuleManager.cs:417`) | **Freeze** after resolution (read-only wrapper and debug assert) | Enumeration order (`BoogieGenerator.cs:773,802`) is the frozen discovery order |
| T14 | Program reporter (`ObservableErrorReporter`) | `IllegalTriggerRemover` (`BoogieGenerator.cs:971`) and the other translator errors | **Buffer** per translation | Publish at commit, in canonical order. This reuses the window's deferral (`CliCompilation.cs:391-401`) |
| T15 | `Options.OutputWriter.Status(...)` at verbose log level (`BoogieGenerator.cs:755-757`) | Start of each translation | Buffer with T14 | Same |
| T16 | Plugin rewriters' `PreVerify` (`BoogieGenerator.cs:759-763`) | Each translation | **Keep serial**: with any plugin loaded, `--translation-workers` falls back to 1 | — |
| T17 | AST lazy caches (`sanitizedName`, `FullSanitizedName`, `CompileNameShadowable`, ...; 29 `??=` sites under `Source/DafnyCore/AST`) | First reader | Benign: idempotent pure values, and reference writes are atomic. List them in the PR and test them under the stress schedule | — |
| T18 | `DafnyOptions` and `TranslatorFlags` | Read only during translation | Freeze (read-only) | — |
| T19 | `program.CardinalityValidationReceipt` (`BoogieGenerator.cs:1013-1016`) | Read only | — | — |
| T20 | `Bpl.Expr.True/False` singletons (`B:Source/Core/AST/Expression/AbsyExpr.cs:485-492`) | Shared across programs. Boogie type checking writes the same `Type` again | Benign (same value). Assert in a stress test | — |
| T21 | Translator instance state (`BoogieGenerator.cs:2005-2032`, `functionHandles`, `letSuchThatExprInfo`, `tytagConstants`, `fieldConstants`, `reportedIllegalTriggerTerms` `:901`) | Per instance | Already private | — |
| T22 | `Bpl.QuantifierExpr.SkolemId` assigned at construction from a static counter (`B:Source/Core/AST/AbsyQuant.cs:585-607`), including when Dafny builds Boogie quantifiers | Every quantifier construction in the process | **Eligibility** (B2) | — |

**Boogie preparation (`GetVerificationTasks`).** Each module's `Bpl.Program` is a separate object graph:
the prelude is parsed per translator (`BoogieGenerator.cs:109,711`), and Dafny keeps no static Boogie
nodes (grep of `Source/DafnyCore` for static `Bpl.*` fields finds none). What two concurrent
preparations share is the following.

| # | Item (B: path:line) | Effect on output | Change |
|---|---|---|---|
| B1 | `Absy.CurrentAbsyNodeId`, interlocked (`B:Source/Core/AST/Absy.cs:53-56,89`) | It reaches names only through monomorphization (`B:Source/Core/Monomorphization.cs:577,1378,1826`). Dafny sets `TypeEncoding.Arguments` (`Source/DafnyCore/DafnyOptions.cs:343`) | Eligibility: require `TypeEncodingMethod == Arguments` |
| B2 | `QuantifierExpr.SkolemIds` (`B:Source/Core/AST/AbsyQuant.cs:585-607`) → `VCQuantifierInfo.uniqueId` (`B:Source/VCExpr/Boogie2VCExpr.cs:665`) → `:skolemid` in SMT only if `EmitDebugInformation` (`B:Source/Provers/SMTLib/SMTLibLineariser.cs:476-478`) | Dafny sets `EmitDebugInformation = false` (`DafnyOptions.cs:345`). With it true, SMT already depends on process history today | Eligibility: require it false. Optional Boogie PR to make it program-scoped |
| B3 | `TypeProxy.proxies`, non-atomic (`B:Source/Core/AST/AbsyType.cs:1953,1978-1979`) | Proxy names appear in type-error messages, never in SMT | Boogie PR: `Interlocked.Increment` |
| B4 | `Type.bvtypeCache`, lazily initialized without a lock (`B:Source/Core/AST/AbsyType.cs:380-395`) | A race can produce two `BvType` objects for one width | Boogie PR: `Lazy<>` or initialize statically. Until then, eligibility probes widths before parallel use |
| B5 | `Split.currentId` (`B:Source/VCGeneration/Splits/Split.cs:62,101`) | Incremented, never read | None |
| B6 | `QuantifierInstantiationEngine.labelToTypes` (`B:Source/VCExpr/QuantifierInstantiationEngine.cs:54,178,826-829`) | Process-global pool table. Used only with `{:pool}` | Eligibility: no `{:pool}` in the program |
| B7 | Option toggles `PrintUnstructured`/`PrintDesugarings` (`B:Source/VCGeneration/ConditionGeneration.cs:729-733`; `B:Source/VCGeneration/VerificationConditionGenerator.cs:452-460` under `lock(Options)`; `B:Source/ExecutionEngine/ExecutionEngine.cs:166-169`) | Only with print/trace options | Eligibility: no `/printFile`, `/printPassive`, `/trace*`, `--bprint`. `--bprint` itself is printed before preparation (`DafnyProgramVerifier.cs:86-90`) and is fine |
| B8 | `Options.TypeEncodingMethod` written (`B:Source/ExecutionEngine/ExecutionEngine.cs:512-515`) | Only in `ResolveAndTypecheck`, not on `GetVerificationTasks` | None |
| B9 | `ImplIdToCancellationTokenSource` static (`B:Source/ExecutionEngine/ExecutionEngine.cs:82`) | Keyed by implementation id. Used at enqueue, not in preparation | None |
| B10 | `LambdaHelper` lambda ids (`B:Source/Core/LambdaHelper.cs:151-160`) | Per visitor | None |
| B11 | `VerificationConditionGenerator.implementationData` (`B:Source/VCGeneration/VerificationConditionGenerator.cs:35`) | `ConditionalWeakTable` keyed by implementation | None (thread-safe, per object) |
| B12 | `CheckerPool` (`B:Source/VCGeneration/CheckerPool.cs:12`) | Only stored by the generator during preparation | None |

No Boogie static blocks concurrent `GetVerificationTasks` under the eligibility conditions (B1, B2, B6,
B7, B4 until fixed). The study's worry about a `CommandLineOptions.Clo` singleton does not apply on
this path.

### 2.3 Mechanism: speculative translation, ordered commit

Each translation k runs on a worker with a **`TranslationContext`**. The context does three things:

- It routes every access to shared naming state through a journal (T1–T5).
- It owns a local proof-dependency manager (T8, T9), a buffered reporter (T14, T15) and the privatized
  caches (T6, T7, T12).
- It records the Boogie nodes that hold placeholders.

At the type level, `Declaration.IdGenerator` and the `uniqueName` fields become reachable only through
a `NamingStore`. That store has three operations, so that a missed call site fails to compile:

```
string Memo(IVariable|Label v, Declaration genOwner, string prefix)   // E1
string Counter(Declaration genOwner, string prefix)                   // E2, returns a placeholder when parallel
void   Push(Declaration)/Pop(Declaration)                             // overlay-local
```

**Event classes.**

- **E1, memoized names.** The event is `(v, owner, prefix, depth, valueUsed)`. If the overlay has no value
  for `v`, the store reads the committed `v` under the store lock. It returns the committed name if there
  is one. Otherwise it allocates from the overlay copy of owner's generator, whose counts are copied on
  first touch of `(owner, prefix)`, and writes the result to the overlay.
- **E2, bare counters at depth 0.** The store returns a placeholder and records the event and the holder nodes.
- **E3, proof-dependency ids.** As T8.

**Commit k** runs only after commit k-1, on one sequencer:

1. Replay the journal in order against the committed store. Each E1 event takes the serial value, which
   is the committed name or a fresh allocation from the committed generator at that prefix, and depth 0
   is applied to the committed counts. If the serial value differs from `valueUsed`, abort and go to step 5.
2. Each E2 event takes the serial value from the committed counter and patches its holders.
3. E3: as T8.
4. Assert that no `\u0001` remains in the program. This is one attribute and identifier visitor,
   `O(|B_k|)`; a leftover placeholder means a copy escaped the recorder. Then publish the buffered
   diagnostics, mark k committed, and release k to preparation.
5. **Mismatch:** throw away k's program, overlay and buffers, and translate k again in direct mode on the
   sequencer thread. Direct mode means no overlay, writing the committed store. Translations 1..k-1 are
   committed and no other translation writes the store, so this run is the serial run.

**Why E1 values may be compared rather than validated by version.** Translation reads a name in exactly
three ways:

- it emits the name as a string;
- it keys translator-local maps by name, which is a bijection when names are unique
  (`DefiniteAssignment.cs:52-193`);
- it **orders by name** (`ExpressionTranslator.cs:1611,1843,1887`) or **tests for null**
  (`TrLoop.cs:209`).

A run whose every returned value equals the serial value therefore takes the serial control path and
emits the serial text. A version-based check would reject the common case: two modules that first-touch
an imported function's formals from the same generator state compute the same names. E2 values are
never Dafny `UniqueName`s, so no ordering or test reads them, and placeholders are safe. The PR must
prove this per call site. It has 58 `FreshId`/`FreshNumericId` sites and 66 `.IdGenerator` references
in `Source/DafnyCore/Verifier`, and the `NamingStore` types make it checkable.

**Equivalence argument.** Let `S_k` be the serial run of translation k, from committed state
`Σ_{k-1}`. The parallel run `P_k` is a deterministic function of the immutable resolved program, the
frozen system catalogue (T13), its private state, and the sequence of values the `NamingStore`
returns. After a successful replay, that sequence equals the one `S_k` would receive from `Σ_{k-1}`.
For E1 this is checked value by value. For E2 and E3 the values are irrelevant to control flow and are
replaced by the serial values. So `P_k` after patching equals `S_k` as a Boogie AST, and `Σ_k` equals
the serial `Σ_k`. Induction on k gives exact equality for every module. A mismatch falls back to `S_k`
itself.

Uniqueness of names within a program, which is the soundness-relevant property (§9 R1), is inherited
from serial.

**Mode H (history-free), a by-product for caches.** The same store can run translation k against
`Σ_0`, the state with no earlier translations, instead of `Σ_{k-1}`. The result is a history-free
program for module k. That is the only sound basis for keying translations by module content (§1), but
it changes names. It is therefore **not** F1, which must equal serial. It is a separate opt-in for B3/D3
to consider.

### 2.4 Scheduler and composition with the module window

```
CliCompilation admission (K modules, canonical order)       unchanged: CliCompilation.cs:391-540
  └─ VerifyCanVerify → Compilation.VerifyUnverifiedSymbol    unchanged ticket order: Compilation.cs:397,435,461
       └─ DafnyProgramVerifier.GetVerificationTasksAsync(module, canonicalIndex)
            ├─ translate stage: worker pool of size W (speculative, LargeStackFactory threads)
            ├─ commit stage: one sequencer in canonical index order (replay / patch / publish / re-translate)
            └─ prepare stage: pool of size Wp (GetVerificationTasks), only if eligible; else Wp = 1
```

- **Canonical index.** `CliCompilation` passes the slot index of `modules` (`CliCompilation.cs:396-399`)
  through `Compilation.VerifyCanVerify` to the verifier. The language server has no global order; there
  the index is the arrival order under the sequencer lock. That is serial-equivalent to the arrival
  order, and that is what the language server does today.
- **No head-of-line deadlock.** The commit for k waits only on commit k-1, never on verification. The
  window admits in canonical order, so translations k..k+K-1 exist only when 1..k-1 have been admitted
  and their translations requested.
- **Effect without the window is nil.** With K = 1 only one module is ever requested at a time. F1
  requires K ≥ 2, and `W ≤ K` (W > K is wasted). Recommended default when on: `W = min(K, C, 4)`.
  Each translation is single-threaded and allocation-heavy; the cap of 4 bounds GC pressure until
  measured.
- **Cancellation.** Speculative work for slot j is cancelled when the window abandons j
  (`CliCompilation.cs:522-537`). A cancelled k cannot be committed, so the sequencer skips it only if the
  window no longer needs any later module either. That is the window's existing rule: modules past an
  abort are dropped.
- **Ineligible runs** fall back to W = Wp = 1, through the old code path, so the output is byte-identical
  by construction:
  - a plugin is loaded (T16);
  - `EmitDebugInformation` is set (B2);
  - the type encoding is not `Arguments` (B1);
  - `{:pool}` occurs (B6);
  - print/trace options are given (B7);
  - `/verifySnapshots` is used.

**Throughput.** The translate-and-prepare chain is a single server today, with
`Wall ≥ max( Σ_m(τ_m+π_m), Σ_m σ_m / C, max_m(τ_m+π_m+σ*_m) )` (σ* is m's longest task chain). With F1:

```
Wall_F1 ≥ max( Σ_m c_m ,  (1+f)·Σ_m τ_m / W ,  Σ_m π_m / Wp ,  Σ_m σ_m / C ,  max_m(τ_m+c_m+π_m+σ*_m) )
```

- `c_m` is the commit cost: replay is O(journal), patching is O(placeholders), and the scan is `O(|B_m|)`.
- `f` is the fraction of translations re-run after a mismatch.

The **go/no-go** is the ratio `ρ_T = C·Σ(τ_m+π_m) / Σ σ_m`, measured on Std and the large downstream
corpus at the core counts in use:

- If `ρ_T ≤ 1`, the solver is the bottleneck. F1 can only shorten the time to the first task, by at most
  `τ_1 + π_1`, and should not be built.
- If `ρ_T > 1`, the expected gain is `Σ(τ+π) − max(Σc, (1+f)Στ/W, Σπ/Wp, Σσ/C)`.

`f` comes from a dry-run mode of the commit stage: translate with overlays serially, replay, count
mismatches. It costs one serial translation and is part of PR F1-2. `c_m/τ_m` is measured in the same
run.

**Memory.** F1 adds no new resident class:

- Journal bytes per module, `J_m ≈ 48 B × (events)`, with events at most the number of `AssignUniqueName`
  plus `FreshId` calls.
- Up to W programs that are translated but not committed, which are already within the window's admitted set.

The peak stays bounded by the window's: `A_res + A_clone + Σ_{m∈admitted}(B_m + T_m) + Σ J_m`. One thing
changes. Translations finish sooner, so the heap rises between two admission checks
(`CliCompilation.cs:401-419`). The admission predicate must add an estimate `B̂` for each in-flight
translation: `GC.GetTotalMemory + (#translating)·B̂ < budget`, with `B̂` the running mean of `B_m`.
The window already raised the standard library's peak RSS 2.6–4.5×. F1 must be measured at K=2,4,8 for
peak RSS, and must not raise it beyond the K-window baseline by more than `W·B̄`.

### 2.5 Acceptance tests

1. **Exactness.** For each corpus × both resolvers (`--type-system-refresh` off/on) ×
   `--translation-workers 1,2,4,8` × K ∈ {2,4,8}, compare against serial (`W=1`, K=1):
   - `--bprint` files byte-equal;
   - SMT logs (`--boogie /proverLog:@PROC@.smt2`) byte-equal under `/deterministicLiteralHashes:1 /restartProver`;
   - `--log-format json` byte-equal after removing timestamps;
   - the verification coverage report byte-equal;
   - verdicts and resource counts equal.

   The corpora are the litTests that have more than one module, Std, and the large downstream project
   (privately, per `AGENTS.md`).
2. **Forced interleavings.** A test hook (`TranslationContext.Yield(point)`) at every store operation,
   driven by a seeded scheduler. Run 200 seeds per stress program, plus the adversarial schedules below:
   - reverse canonical completion;
   - module k finishing before every j<k;
   - two modules first-touching the same imported formals;
   - the owner module translated last.
3. **Stress programs**, generated under `Source/IntegrationTests/TestFiles/LitTests/LitTest/pipeline/`:
   - `chain(n)`: `M_i` imports `M_{i-1}`;
   - `wide(n)`: n modules import one base with 50 functions used as values (exercises `$fh$`);
   - `diamond`;
   - refinement with inherited bodies that need re-verification (`MustReverify`);
   - export sets with opaque/reveal;
   - let-such-that inside imported function bodies (T6);
   - labelled asserts and `old@L` (T5, T7);
   - a nonzero carry-over of the current proof-dependency definition (T9).
4. **Mismatch path.** Inject a mismatch: a test option makes the overlay allocate at count+1. Show that the
   output is still exact and that `f` is reported.
5. **Option off.** Full suite and Std verdict gates byte-identical (`.github/review/expected-verdicts.tsv`,
   `expected-std-verdicts-z3-*.tsv`).

### 2.6 Staged PRs

| PR | Content | Files | LOC | Risk |
|---|---|---|---|---|
| F1-0 | Privatize T6, T7, T12. Lock T10. Freeze T13 (debug assert). No behavior change | `LetExpr.cs`, `AssertLabel.cs`, `BoogieGenerator.LetExpr.cs`, `Methods.cs`, `Iterators.cs`, `TrPredicateStatement.cs`, `Functions.Wellformedness.cs`, `VisibilityScope.cs`, `BoogieGenerator.cs` | ~200 | low; the output of every test must be unchanged |
| F1-1 | `NamingStore` API; move T1–T5 behind it in **direct mode only** (same calls, same order) | `Declaration.cs`, `NonglobalVariable.cs`, `LocalVariable.cs`, `Label.cs`, `FreshIdGenerator.cs`, ~40 call sites in `Source/DafnyCore/Verifier/**` | ~600 | medium: mechanical, but wide |
| F1-2 | Overlay mode, journal, replay and patcher; local `ProofDependencyManager`; buffered reporter; **dry-run measurement** of `f`, `c_m` and `J_m` behind a hidden flag | new `Verifier/TranslationContext.cs`, `ProofDependencyManager.cs`, `ObservableErrorReporter.cs` | ~900 | medium-high: the correctness core |
| F1-3 | Scheduler in `DafnyProgramVerifier`; canonical index plumbed from `CliCompilation`; eligibility; `--translation-workers`; admission estimate; docs, help, news | `DafnyProgramVerifier.cs`, `IProgramVerifier.cs`, `Compilation.cs`, `CliCompilation.cs`, `VerifyCommand.cs`, `docs/DafnyRef` | ~500 | medium |
| F1-4 | Parallel preparation (Wp). In Boogie: B3 and B4 fixed in erniecohen/boogie plus a separate adoption PR (study D1) | Boogie `AbsyType.cs`; Dafny repin | ~40 + repin | low code, process-heavy |
| F1-T | Stress generator, interleaving hook, CI job comparing W=1 vs W∈{2,4,8} | `.github/workflows/`, `.github/review/` | ~400 | — |

F1-2's dry run is the first deliverable. It measures `f` and `c_m` before any concurrency exists.

---

## 3. F2 — Parallel module resolution (D8)

### 3.1 Current structure

`ProgramResolver.Resolve` (`Source/DafnyCore/Resolver/ProgramResolver.cs:33-97`) runs these steps:

- bind names and build the dependency graph (`:47`, `:124-148`). The graph covers imports, refinement,
  exports, abstract imports and nesting, because "parent modules implicitly depend on submodules"
  (`:146`);
- topological sort (`:53`) and heights (`:56`);
- system module (`:58-61`);
- program-level `PreResolve` rewriters (`:63-69`);
- then, **per module in topological order**, `ResolveModuleDeclaration` (`:71-75`) with a fresh
  `ModuleResolver` and its own `BatchErrorReporter` (`ModuleResolver.cs:207-218`). That call returns a
  `ModuleResolutionResult` (`ModuleResolver.cs:300-324`), and `ProcessDeclarationResolutionResult`
  (`:104-118`) commits it;
- afterwards, serial global passes (`:81-96`): replaceable modules, duplicate names, program
  `PostResolve`, cardinality validation.

The per-module result is already a private object committed in order. This is the seam that F2 uses
and that `CachingResolver` already exploits (`Source/DafnyLanguageServer/Language/CachingResolver.cs:68-80`).
Per-module rewriters are created per module with the module's reporter
(`Source/DafnyCore/AST/Modules/LiteralModuleDecl.cs:125`).

### 3.2 Inventory of shared mutable state reached by module resolution

| # | Item (path:line) | Writer, when | Change | Order recovery |
|---|---|---|---|---|
| R1 | `Program.ModuleSigs` (`ProgramResolver.cs:54,109`) | Commit | Keep at commit (canonical order). Workers do not read it (only `ProgramResolver` uses it) | Commit order |
| R2 | `ProgramResolver.classMembers` (`:14,25-27,60,101,113`) | System module; `AddSystemClass` (bitvectors, `ModuleResolver.cs:2543`); commit | Read by other modules' resolvers through `GetClassMembers` (`ModuleResolver.cs:198-203`, 20+ callers in both resolvers). Make it a `ConcurrentDictionary`. **Publish a module's tables at worker completion**, before dependents start (task completion gives happens-before). Never enumerated | None needed (lookup only) |
| R3 | `moduleDeclarationPointers` setters (`ProgramResolver.cs:106`, built `:139-140,281-300`) | Commit; writes the resolved decl into the **enclosing** module's lists | Run at worker completion under the coordinator lock. The parent depends on the child (nesting edge), so the parent has not started. **Audit:** no sibling reads the parent's `TopLevelDecls` during its own resolution (name lookup goes through `ModuleBindings`). The stress test "nested siblings" checks it | — |
| R4 | `SystemModuleManager.Bitwidths` (`AST/SystemModuleManager.cs:72`), a `HashSet<int>` | `NameResolutionAndTypeInference.cs:4158` (every bitvector type resolved), `Rewriters/BitvectorOptimization.cs:40` | **Intern + discovery log**: a worker records its first touches of keys missing from the pre-state | At commit, append the logged keys not yet committed. After the last commit, rebuild the set by inserting in that order. With no removals, `HashSet` enumeration equals insertion order, the order translation uses (`BoogieGenerator.cs:773`) |
| R5 | `SystemModule.SourceDecls` (bitvector `ValuetypeDecl`s `ModuleResolver.cs:2535-2545`; tuples `SystemModuleManager.cs:409-440`; arrows `:286`; arrays via `ArrayType`, `ModuleResolver.cs:3428`, `PreTypeResolve.cs:70-72`) and the tables `arrayTypeDecls`, `ArrowTypeDecls`, `PartialArrowTypeDecls`, `TotalArrowTypeDecls`, `tupleTypeDecls` (`:15-19`) | Late creation during module resolution: `NameResolutionAndTypeInference.cs:4384`, `PreTypeResolve.Statements.cs:1155`, `PreTypeResolve.cs:74-79` | **Intern** in a `BuiltinCatalog` (one lock; create-or-get; the object depends only on its key). **Discovery log** as R4 | Rebuild `SourceDecls`: pre-state prefix, then committed discoveries in canonical order. Translation enumerates it at `BoogieGenerator.cs:802` |
| R6 | `SystemModuleManager.PreTypeBuiltins` (`:28`), shared by every refreshed resolver (`PreTypeResolve.cs:723`) | `PreTypeResolve.cs:78,109,136`. The first adder also runs `FillInPreTypesInSignature(bvDecl)` (`:79-81`), a mutation of the shared declaration | Intern. **The first-toucher's initialization runs inside the catalog lock.** The PR must show that the initialized content does not depend on which module's resolver runs it. If it does, that key is *order-sensitive* and triggers R-restart (§3.4) | As R5 |
| R7 | `MaxNonGhostTupleSizeUsed/Token` (`SystemModuleManager.cs:32-33,432-435`) | First creator of a larger tuple | Recompute at commit from the canonical discovery log | Canonical log |
| R8 | `SystemModuleManager.MyHash` (`:35-63`) | Lazy | Compute after the last commit. The value is order-independent (sorted keys) | — |
| R9 | `VisibilityScope.maxScopeId` (`AST/VisibilityScope.cs:11,66-74`) | `new VisibilityScope(name)`: `ModuleResolver.cs:616`, `ModuleExportDecl.cs:33,50`, `ModuleDefinition.cs:185` | `Interlocked.Increment`. Ids are only compared for equality (overlap tests). They never reach output, so their values need not be canonical | — |
| R10 | `ModuleDefinition.visibilityScope ??=` (`AST/Modules/ModuleDefinition.cs:184-185`) | First reader | Force creation in the serial graph phase. A lost race creates two scopes, so visibility would be wrong | — |
| R11 | `VisibilityScope.scopeTokens/cached` of **imported** declarations | Importers augment imported declarations' scopes (`ModuleResolver.cs:616-623`; `RevealAllInScope` `:1062-1076`; `:362-366,469-479,535,762-763,1131-1132`). `VisibleInScope` reads also write | **Lock** per scope around `Augment` and `VisibleInScope` (T10's lock). Set union commutes and the memo is pure | None needed |
| R12 | `Type._scopes/scopesEnabled` (`Types.cs:36-40`) | `ProgramResolver.cs:36-38,79` on the coordinator thread | Each worker: `ResetScopes; EnableScopes` … `DisableScopes` in `finally`, synchronously on one `LargeStackFactory` thread | — |
| R13 | `TypeRefinementWrapper.count` (`Resolver/PreType/TypeRefinementWrapper.cs:13-14`) | Every wrapper | `Interlocked`. Used only in debug strings (`:65`) | — |
| R14 | `QuantifierExpr.currentQuantId` (`AST/Expressions/Comprehensions/QuantifierExpr.cs:15,42-44`) | Interlocked already. `FullName` "q$N" reaches no output (grep) | None | — |
| R15 | `Type.reusableNewtypeWalkSet` (`Types.cs:177-190`) | Already rent/return with `Interlocked` | None | — |
| R16 | Diagnostics | Module `BatchErrorReporter` (`ModuleResolver.cs:217`); replayed at commit (`ProgramResolver.cs:116-118`) | Keep. Error-count checks inside a module are relative to the module's own reporter (`ModuleResolver.cs:1195-1398`) | Commit order = serial order of messages |
| R17 | Per-module rewriters (`LiteralModuleDecl.cs:125-183`) | Fresh instances per module with the module reporter. 11 rewriter classes have instance fields (`RefinementTransformer` 13, `AutoReqFunctionRewriter` 8, ...); no static mutable fields (grep) | Keep per module. **Plugins** (`Plugin.cs:13,23,60`) fall back to serial | — |
| R18 | Program-level rewriters' `PreResolve(Program)`/`PostResolve(Program)` (`ProgramResolver.cs:63-69,86-89`) | Before and after the per-module loop | **Keep serial** | — |
| R19 | Cardinality validation (`ProgramResolver.cs:93-96`); replaceable modules, duplicate names (`:81-82`) | After the loop | **Keep serial** (study §6.6: cardinality has reverse edges) | — |
| R20 | Legacy inference proxies in imported signatures | A completed module's types may still contain `TypeProxy` objects whose `T` is set; `Normalize` is pure (`Types.cs:147-157`) | Invariant, checked in debug builds at worker completion: no reachable `TypeProxy` with `T == null` in a published signature. Importers then only read | — |
| R21 | Refinement: `RefinementTransformer` clones the refinement base | Reads the completed base | Base → refiner is a graph edge, so the base is complete. Nothing mutates it until the serial postpasses | — |

### 3.3 Scheduler

The coordinator thread does three things in order:

1. Run the existing serial prefix (`ProgramResolver.cs:34-69`), with R10 forced.
2. Snapshot the catalogue pre-state.
3. Start the scheduler over `dependencies` (`ProgramResolver.cs:16`). It is the same graph as the topological
   sort, so it already has every edge kind.

A module is ready when all its successors (dependencies) have **completed**; they need not be
committed. Completion means R2 and R3 are published. N workers resolve ready modules. **Priority** is the
longest remaining weighted path, with weights `r_m` from the previous run or else 1, and ties broken by
canonical index. Commit is a separate in-order cursor over `sortedDecls`. It handles R1, R4–R8 replay
and R16, and never blocks a worker.

**Bound.** With `c_R` the per-module commit overhead and `q` the restart probability (§3.4):

`R_N ≥ R_g + max( (W_R + M·c_R)/N , L_R ) + q·(R_g + W_R)`.

**Go/no-go.** The measurement worker supplies `W_R/L_R` (the DAG's parallelism) and the front-end share
of wall time, `φ = (P + R_g + W_R)/Wall`. The gain is
`φ·(1 − (R_g + max(W_R/N, L_R))/(R_g + W_R))`. If `W_R/L_R < 1.5` or `φ < 0.1`, the ceiling is below about
3% of wall time per the formula; do not build. A chain graph has `L_R = W_R` and gains nothing.

### 3.4 Restart on late, order-sensitive builtin discovery

Interning makes most builtins order-insensitive. A worker that needs `bv7` gets the same declaration
object whoever created it. The discovery log restores the order in which translation sees them (R4,
R5). A restart is needed only when **resolution behavior** depends on the catalogue state. That
happens in two cases:

- R6, if first-touch initialization depends on the initializer;
- a worker enumerating the catalogue. The one site, `ModuleResolver.cs:2521`, uses `.Find` by predicate,
  which is order-independent while widths are unique; the PR must assert this.

Each such site is marked `OrderSensitive(key)`. If a worker hits one for a key that, in canonical order,
an *earlier* module discovers after the worker read the catalogue, the coordinator restarts:

1. Cancel all workers.
2. Discard every uncommitted result and its buffered diagnostics.
3. Restart **serially** from pristine input, a fresh clone of `programAfterParsing`
   (`Compilation.cs:262-263`), with a fresh `SystemModuleManager`.

Continuing serially on partly mutated state is never correct (study §6.2). The restart cost is
`R_g + W_R`, so `q` must be measured. The dry-run mode records hits without restarting. If `q > 0` on Std
or the downstream corpus, R6 must be redesigned before F2 ships.

### 3.5 Legacy vs refreshed resolver

| Concern | Legacy | `--type-system-refresh` |
|---|---|---|
| Builtins | R4, R5 via `NameResolutionAndTypeInference` | R4, R5, **R6** via `PreTypeResolve`; `PreTypeBuiltins` shared program-wide |
| Inference state | Per `ModuleResolver` (scopes `ModuleResolver.cs:211-214`) | Per module `PreTypeResolver` and `PreTypeInferenceModuleState` (`PreTypeResolve.cs:436-440,723`); module attributes still use legacy (study §6.7) |
| Escaping state | R20 (proxies) | No unresolved `PreTypeProxy` after `PreTypeToType`; assert at completion |
| Restart triggers | `ModuleResolver.cs:2521` only | Also R6 |

Both resolvers run the full acceptance matrix.

### 3.6 Acceptance tests

1. **Exactness.** For each corpus × both resolvers × N ∈ {1,2,4,8}, compare against serial:
   - diagnostics byte-equal (`resolve`, `verify --no-verify` where available, and the JSON format);
   - `SystemModule.TopLevelDecls` names in order equal, and `Bitwidths` order equal;
   - `--bprint` of every module byte-equal (catches any builtin order drift);
   - `dafny format` unchanged;
   - cardinality receipts equal.
2. **Graphs**, generated:
   - `chain(64)`, `wide(64)`, `diamond(k)`;
   - refinement chains with abstract imports;
   - export sets (`provides`/`reveals`, `extends`);
   - nested modules with sibling imports (R3);
   - a module that introduces `bv13`, a 5-tuple and arity-7 arrows first, with a canonically earlier module
     introducing them **later** in time (forced by priority inversion);
   - both resolvers.
3. **Rejected programs.** Cycle errors (`ProgramResolver.cs:143-147`), errors in a dependency that
   cascade, duplicate module names. Diagnostics must be equal and the receipt absent.
4. **Interleavings.** A seeded scheduler at catalogue and publication points; 200 seeds.
5. **Restart path.** Inject `OrderSensitive` hits. Output equals serial; `q` is reported.
6. **Caching interplay.** `CachingResolver` subclass with N > 1: the cache hit path returns committed
   results unchanged.

### 3.7 Staged PRs

| PR | Content | Files | LOC | Risk |
|---|---|---|---|---|
| F2-0 | R9, R10, R11 (locks), R13. No behavior change | `VisibilityScope.cs`, `ModuleDefinition.cs`, `TypeRefinementWrapper.cs` | ~80 | low |
| F2-1 | `BuiltinCatalog` (intern, discovery log, order rebuild), used serially; R2 concurrent dictionary | `SystemModuleManager.cs`, `ModuleResolver.cs`, `NameResolutionAndTypeInference.cs`, `PreTypeResolve*.cs`, `BitvectorOptimization.cs`, `ProgramResolver.cs`, `CachingResolver.cs` | ~500 | medium: the order rebuild must be the identity when serial |
| F2-2 | Dry-run: resolve serially, but record every `OrderSensitive` read and simulate a parallel schedule to compute `q`; record `r_m` and `L_R` | `ProgramResolver.cs` | ~250 | low |
| F2-3 | Scheduler, workers, completion publication (R2, R3), restart; `--resolver-workers` | `ProgramResolver.cs`, `Compilation.cs`, options, docs, news | ~700 | high |
| F2-T | Stress generator and CI matrix | workflows | ~400 | — |

---

## 4. F3 — Resolution caching

### 4.1 (a) `CachingResolver`-style reuse in the CLI

**Fact.** The CLI already resolves through `CachingResolver`. `CliCompilation.Create` builds
`DafnyLangSymbolResolver` (`CliCompilation.cs:112`), and that class selects `CachingResolver` when
`--use-caching` is true, the default (`Source/DafnyLanguageServer/Language/Symbols/DafnyLangSymbolResolver.cs:18,51-53`).
The cache is an instance field of that resolver (`:34`), created once per compilation. A one-shot
`dafny verify` therefore resolves every module once, with a cold cache, and pays the hashing
(`CachingResolver.cs:84-108`) for no hit. The cache's `ModuleResolutionResult`s are an additional root
of the whole resolved AST (§5).

**What reuse can remove, by scenario.**

| Scenario | Removed front-end time | Notes |
|---|---|---|
| One-shot CLI | 0, plus the hash cost `h·M` | Nothing to reuse |
| One process, several compilations: watch mode, batch requests (`repeatVerification`, `CliCompilation.cs:267-277`), a driver that verifies several projects sharing Std | `Σ_{m∈H} r_m − M·h − |H|·x` per later compilation | H: modules whose key (dependency hashes, builtin hash, `CloneId`) is unchanged; x: per-hit materialization |
| Same, with study §6.3 content keys instead of `CloneId` | Larger H: a file edit no longer invalidates every module in that file | Requires §6.3 items 1–6 |

The fraction of front-end time removed per reused compilation is
`F_a = (Σ_{m∈H} r_m − M·h − |H|·x) / (P + R_g + W_R)`. It is bounded by `W_R/(P+R_g+W_R)`, because
`P` (parse is cached separately by `CachingParser`) and `R_g` (cardinality, postpasses) are not removed.
`R_g` includes whole-program cardinality validation, which the code deliberately re-runs on every hit
(`ProgramResolver.cs:91-96`).

**Design.** There is no new CLI cache. Make the existing one useful only where a process resolves more
than once:

1. Lift `ResolutionCache` out of `DafnyLangSymbolResolver` into a process service that the CLI batch
   driver injects into every `Compilation` it creates.
2. Bound it with the existing prune-on-use policy (`PruneIfNotUsedSinceLastPruneCache`).
3. Add `--resolution-cache-content` (study §6.3) for content keys.
4. For one-shot runs, add `--use-caching false` to the measured profile. That removes `M·h` and a root
   (§5). The default stays as it is.

**Go/no-go.** Measure `h`, `x` and `r_m`. Build only if the target workflow is multi-compilation.
For a one-shot `verify`, F3a is a pure cost.

### 4.2 (b) Persistent typed `.doo` library snapshots (D6)

Today a `.doo` holds the program text and a manifest (`Source/DafnyCore/DooFile.cs:28-37,102-104,143-155`).
Loading validates the manifest (`:165-178`) and **reparses and re-resolves** the text, so a library costs
`P_lib + Σ_{m∈lib} r_m` in every process.

**Schema.** The snapshot is a sidecar, not inside the `.doo`. It is versioned and holds a self-describing
binary graph of *resolved declarations*, never CLR object graphs.

- **Header:** schema version; Dafny product version and assembly hash; resolver engine (legacy or
  refreshed) and every option that affects resolution (taken from the manifest option set plus the
  resolver flags); a SHA-256 of the `.doo` bytes; the builtin catalogue keys the library discovered, in
  discovery order (§3.2 R4/R5); plugin list (must be empty); producer identity.
- **Body:**
  - per module: the export sets with their `provides`/`reveals` membership, the signatures, and the
    class member tables (R2);
  - per declaration: resolved types with **solved proxy targets**, with newtype, subset, opaque and
    facade identity kept (no unrestricted expansion; study §6.4);
  - per declaration: the type-parameter characteristics, inferred decreases, autoreq contracts, triggers
    and the attributes after rewriters;
  - function bodies and contracts visible through any export;
  - cardinality and termination summaries needed by global admission;
  - rewriter-generated declarations;
  - stable symbol handles (module path + declaration path + overload index) for rebinding;
  - source ranges as (file id, offset) into the `.doo` text.

**Key.** The full header. The `.doo` hash covers the library's text and manifest. The client's options
must equal the library's recorded options where `CheckAndGetLibraryOptions` (`DooFile.cs:181`) requires
equality, and must equal *all* resolution-affecting options for the snapshot. That is a stricter check
than `.doo` compatibility.

**Trust.** A snapshot asserts type facts, such as "`x` has type `nat`" or "this function's body is
visible". The verifier trusts them without re-deriving them, and a false one can make an unsound program
verify. So:

- Snapshots load only from a producer the user trusts. One case is local production: the cache directory
  is written by this user's own Dafny, with an HMAC keyed by a per-user secret in the cache directory.
  The other is an explicitly configured signing key, for shared caches.
- A hash match alone never authorizes a load. The hash protects against corruption, not against forgery.
- There is no third-party "verified snapshot" path. Full semantic validation is equivalent to
  re-resolution and erases the gain (study §6.4).

**Rebinding.** The client resolves its own modules against the loaded library modules. Library
declarations are materialized as real AST nodes, with `Origin`s pointing into the library text. The
text stays available from the `.doo`, so diagnostics and hover can render. Handles are resolved
in one pass. Any dangling handle is a miss.

**Validation on load**, all cheap:

1. Header equality.
2. Reference closure: every handle resolves.
3. The builtin keys are merged into the client's `BuiltinCatalog` in the recorded order, before the
   client's modules, which equals serial order because library modules precede their importers
   topologically.
4. Generated-declaration inventory equals the recorded counts.
5. Then global admission (cardinality, `ProgramResolver.cs:93-96`) runs on the whole program as today.
   Admission is never cached.

**Fallback.** Any mismatch, unsupported schema, plugin, corruption or untrusted producer means the source
path is used and a single info diagnostic is printed. It never produces an error that the source path
would not produce.

**What it removes.** Let `ℓ` be the load and decode time, `v` validation and `b` rebinding. Then:

`F_b = (P_lib + Σ_{m∈lib} r_m − ℓ − v − b) / (P + R_g + W_R) ≤ δ_lib`.

`R_g` is not reduced: cardinality runs over the library as well. Unless admission gets per-module
summaries, the ceiling is `δ_lib − R_g,lib/(front end)`. Peak memory changes by
`+(snapshot decode buffers) − A_clone,lib`. The parsed clone of library modules is no longer needed if
F4's change 1 is taken. The measurement worker's `.doo` share `δ_lib` decides: build only if
`δ_lib·φ ≥ 0.05` of wall time on a target workflow, and only after a prototype shows `ℓ+v+b ≤ 0.3·(P_lib+Σ r_lib)`.

**Tests.**

- `.doo` round trips for both resolvers.
- Manifest/option mismatch → miss.
- Changed Std → miss.
- Snapshot built with a different Dafny build → miss.
- Tampered snapshot: an altered type fact with a valid content hash but a bad HMAC → miss, and a
  **negative control** in which the same altered snapshot force-loaded by a test hook makes an
  `assert false` fixture verify. This shows the trust boundary matters.
- Downstream cardinality cycle through a cached trait → error as in source mode.
- Exactness: client `--bprint` and SMT byte-equal to the source path.

**PRs.**

| PR | Content | LOC | Risk |
|---|---|---|---|
| F3b-1 | Snapshot writer and reader for **legacy** resolver; schema v1; local-HMAC trust; fallback | ~2,500 | high (breadth of AST) |
| F3b-2 | Refreshed resolver support | ~800 | high |
| F3b-3 | CLI option and docs; Std build emits a snapshot in CI for measurement only | ~300 | medium |

F3b depends on F2-1 (catalogue with recorded discovery order) and on the §6.3 notion of "resolved imported view".

---

## 5. F4 — Releasing memory early, and one resolved program for in-process parallelism

### 5.1 Roots that keep a module's data alive after its results are reported

| Data | Root (path:line) | Lifetime today | Change |
|---|---|---|---|
| Parsed clone (`A_clone`) | `Compilation.programAfterParsing` (`Compilation.cs:65,262-263`) and the `ParsedProgram` task result (`:73`) | Whole compilation | **Change 1:** in `verify`/`resolve` without formatting or IDE consumers, null the field and complete `ParsedProgram` with a weak handle after `FinishedParsing` is published. Saves `A_clone`, about a full AST |
| Resolved program (`A_res`) | `Compilation.transformedProgram` (`:66`); `Resolution` task (`:74`); `CliCompilation`'s `resolution` local (`CliCompilation.cs:258`); `VerifyCommand` closures (`Source/DafnyDriver/Commands/VerifyCommand.cs:117-118,294,321`) | Whole run | Needed until the **last** translation, because every translation reads all modules (`BoogieGenerator.cs:837-842`). After that, only reporting needs it: origins, proof-dependency nodes, counterexample mapping. See change 4 |
| Resolution cache | `DafnyLangSymbolResolver.resolutionCache` (`DafnyLangSymbolResolver.cs:34`) → `ModuleResolutionResult` per module | Whole run | **Change 2:** in a one-shot CLI, drop the cache after resolution, or don't create it (`--use-caching false`) |
| Module's Boogie program `B_m` | `Compilation.translatedModules` (`Compilation.cs:51-52`); cleared by `ClearModuleCache` (`:802-804`) after the module's symbols are reported (`CliCompilation.cs:365,524`) | Until the module is reported | Fine |
| Tasks `T_m` | `tasksPerVerifiable` (`Compilation.cs:55`), cleared per symbol (`:806-813`); `CanVerifyResult.Results[*].Task` → `Split` → `Implementation` → `Program` | Until subscribers return | Subscribers serialize at once (JSON logger `Source/DafnyDriver/JsonVerificationLogger.cs:142-146` (serialized `JsonNode`s), CSV `CSVTestLogger.cs:13,69`). Fine, if no subscriber keeps `result`. Audit the text logger |
| Idle solver contexts | `Checker.Program` (`B:Source/VCGeneration/Checker.cs:50,166`) in `CheckerPool.availableCheckers` (`B:Source/VCGeneration/CheckerPool.cs:12`) | Until the checker is retargeted | Up to C idle checkers each pin the **last** Boogie program they ran and its prover context. **Change 3 (Boogie PR):** clear `Program` and the context when a checker goes idle and its program's module is released, via a `CheckerPool.Release(Program)` that the Dafny side calls from `ClearModuleCache` |
| Proof dependencies | `ProofDependencyManager` (`ProofDependencyManager.cs:17-18`): `ProofDependency` → Dafny AST nodes (ranges, expressions) | Whole run | Needed for the end-of-run coverage report and the warnings. With neither requested, change 4 may drop module entries after reporting |
| Boogie result cache | `ExecutionEngine.Cache` (`B:Source/ExecutionEngine/ExecutionEngine.cs:49`) | Used only with `/verifySnapshots` | No change |
| Static Boogie tables | B6 `labelToTypes` grows per program | Process | Only with `{:pool}`. Boogie PR: program-scoped |

**Change 4: drop method bodies of reported modules.** Later translations read the specifications and
visible function bodies of earlier modules, never method bodies; translation emits implementations only
for `forModule`. So after module m is reported, with no coverage report and no proof-dependency warnings,
the bodies of m's methods, lemmas and iterators can be detached. Their `Origin`s stay for diagnostics
already published. Saving: `A_body(m)`, the method-body share of `A_res`, which the measurement worker
gets from a heap snapshot by node ownership. This must not run before every translation that reads m is
committed (F1's commit cursor gives that point). It is opt-in (`--release-verified-modules`), and the
output must be unchanged. A later reader of a detached body is a bug, so the hook throws.

Peak memory per process, today and after changes 1–4:

```
Peak_now ≈ A_clone + A_res + A_cache + Σ_{m∈admitted}(B_m + T_m) + C·B_last
Peak_F4  ≈ A_res − Σ_{reported} A_body(m) + Σ_{m∈admitted}(B_m + T_m) + (idle checkers: 0)
```

`A_cache` is mostly shared with `A_res` (same objects) plus hashes and result objects. Its marginal size
is measured, not assumed.

### 5.2 One resolved program instead of N processes

The failure in the issue was 14 processes × more than 5 GB on a 36 GB machine. The resolved program is
held once **per process**, so `N·(A_clone + A_res + …)`. Running N processes was a way to get parallelism
across modules, because one process translates serially and, at K = 1, verifies one module at a time.
The in-process equivalent is one process with `--cores C`, `--verification-module-window K`,
`--translation-workers W` (F1) and change 1–4 releases:

```
Mem_N-proc ≈ N·(A_clone + A_res + B̄ + T̄)          Mem_1-proc ≈ A_res + K·(B̄ + T̄)
```

**Equivalence** to the N-process run. Each process of the N-process run verifies a subset of modules,
so its module k translation follows a different history (§1). The N-process output is therefore *not*
byte-equal to the 1-process output. The 1-process run is equal to the serial 1-process run (F1). This
should be stated, not hidden: the N-process approach is the one that is history-dependent.

**Throughput:**
- N processes have up to `N` translation chains and `N·C` solvers.
- One process has `W` translation chains and `C` solvers.

Matching requires `C_1 = N·C_N` solver threads, and `W ≥ ρ_T`-limited translation (§2.4). A further
limit is the solver side's own scaling in one process (the checker pool and the semaphore
`B:Source/ExecutionEngine/ExecutionEngine.cs:60`). The measurement worker's per-stage timing at
C ∈ {4, 8, 16} decides whether one process saturates the machine.

**PRs.**

| PR | Content | LOC | Risk |
|---|---|---|---|
| F4-1 | Changes 1, 2 (CLI-only, opt-in) | ~120 | low |
| F4-2 | Change 3 (Boogie PR plus adoption) | ~60 + repin | low |
| F4-3 | Change 4, after F1-2 (needs the commit cursor) | ~300 | medium |
| F4-M | Heap-snapshot measurement of `A_clone`, `A_res`, `A_cache`, `A_body`, `B_m`, `T_m` and checker retention, in a workflow | ~200 | — |

---

## 6. Dependencies and order of work

```
F1-0 ─┬─ F1-1 ─ F1-2 (dry run: f, c_m) ─┬─ F1-3 ─ F1-4
      │                                  └─ F4-3
F2-0 ─┴─ F2-1 ─ F2-2 (dry run: q, L_R) ─ F2-3
F2-1 ─ F3b-1 ─ F3b-2 ─ F3b-3
F4-1, F4-2, F4-M: independent, first
F3a: only if a multi-compilation workflow is the target
```

F1-0 and F2-0 share the `VisibilityScope` lock (T10 = R11), so they land together. F4-1 and F4-M
come first: they are small, and they produce the memory numbers that every later go/no-go uses.

| Order | PR | Decides |
|---|---|---|
| 1 | F4-M, F4-1, F4-2 | `A_*`, `B_m`, `T_m`; the 36 GB case |
| 2 | F1-0, F2-0 | (prerequisites, no behavior change) |
| 3 | F1-1, F1-2 | `f`, `c_m`, and with D0's spans `ρ_T` → go/no-go for F1-3 |
| 4 | F2-1, F2-2 | `q`, `L_R`, `W_R`, `R_g` → go/no-go for F2-3 |
| 5 | F1-3/F1-4 or F2-3 | whichever has the larger measured gain |
| 6 | F3b | only if `δ_lib·φ ≥ 0.05` |

## 7. Which measurement decides each go/no-go

| Feature | Decision quantity | Threshold | Source |
|---|---|---|---|
| F1 | `ρ_T = C·Σ(τ+π)/Σσ` at the user's C | build if > 1.2 on a target project | D0 spans `translate`, `prepare`, task solves |
| F1 | `f` (re-translation rate), `c_m/τ_m` | ship if `f < 0.1` and `Σc < 0.1·Στ` | F1-2 dry run |
| F1 | peak RSS at K=2,4,8 with W=K | ≤ K-window baseline + `W·B̄` | process sampler (study §8.2) |
| F2 | `W_R/L_R`, `φ` | `W_R/L_R ≥ 1.5` and `φ ≥ 0.1` | per-module resolution spans + DAG |
| F2 | `q` | `q = 0` on Std and the downstream corpus | F2-2 dry run |
| F3a | `Σ_{H} r_m − M·h − |H|·x` | > 0 in the target multi-compilation workflow | resolver spans with the cache on/off |
| F3b | `δ_lib·φ`, `(ℓ+v+b)/(P_lib+Σ r_lib)` | ≥ 0.05 and ≤ 0.3 | `.doo` share; prototype |
| F4 | `A_clone`, `A_body`, `C·B_last` share of peak | any ≥ 10% of peak | F4-M |

## 8. Soundness summary

None of the four features adds an axiom or an assumption. Each is meant to be **neither** in the
fork's classification. The exactness conditions make every option-on output equal to the serial
output. The routes by which that could fail, and the guard for each:

- **F1:** a name collision inside one Boogie program can capture a bound variable silently, and that is
  unsound. Guard: equality with serial names, which are unique (E1 comparison, E2/E3 serial values,
  placeholder scan); mismatch falls back to serial translation.
- **F2:** a visibility error from a lost scope (R10) or a corrupted set (R11) could expose a hidden body.
  Guard: locks and forced creation; byte-equal Boogie output in tests.
- **F3b:** a forged or stale snapshot asserts false type facts. Guard: trusted producers only, full keys,
  global admission never cached, and a negative control.
- **F4:** a detached body read later. Guard: the hook throws, and translation must not read bodies of
  reported modules.

## 9. Critical review, most severe first

1. **(Severe) F1's exactness rests on capturing every naming effect.** One future AST memo that bypasses
   `NamingStore` makes SMT schedule-dependent, and only a collision of names makes it unsound. Mitigation:
   - make the fields private to `NamingStore`;
   - run the placeholder scan;
   - run a CI job that compares W=1 and W=4 on every PR touching `Source/DafnyCore/Verifier` or
     `Source/DafnyCore/AST`.

   Residual risk: a new memo that is not a name, like T6/T7. The stress interleavings catch only those
   on exercised paths.
2. **(Severe) F1 may not pay.** If the measured `ρ_T ≤ 1` at typical C, solver time dominates and F1 is
   about 3,000 lines of risk for a first-task latency of `τ_1+π_1`. The dry run and D0's spans must come
   first. This design deliberately puts F1-2 (measurement) before F1-3 (concurrency).
3. **(High) Memory can get worse before it gets better.** F1 and F2 both raise concurrency inside a
   process whose problem is already memory. The K-window measured 2.6–4.5× peak RSS on Std. F4-1/F4-2
   must land first, and F1's admission must budget in-flight translations.
4. **(High) R6 first-touch initialization in the refreshed resolver may be order-sensitive.** If so,
   every new bitvector width forces a restart (`q > 0`) and F2 is useless with `--type-system-refresh`.
   This is the configuration the standard library uses. Resolve this in F2-1 before investing in F2-3.
5. **(High) History dependence (§1) invalidates naive cache keys.** It also means the 14-process baseline
   is not byte-comparable with a 1-process run. Any cache PR (B3, B4, D2, D3) must key on history or
   adopt mode H, and that changes output, so it would need its own option and its own equivalence story.
6. **(Medium) Language server.** F1's canonical order there is arrival order. Exactness against the CLI
   is not claimed for the IDE, where serial output already depends on edit history.
7. **(Medium) F3b's trust model limits sharing.** A cache shared across users needs signing keys and key
   management, which the fork does not have. Local-only snapshots help repeated local runs and CI caches
   keyed by runner, not downloads.
8. **(Medium) Eligibility fallbacks hide the feature silently.** A plugin, `{:pool}` or a debug flag
   reverts to serial. Report the fallback reason once at info level, or users will measure "no gain"
   without knowing why.
9. **(Low) Benign races** (T17, T20, B3, B4) are argued, not proved. The interleaving stress tests and
   Boogie's own fixes reduce but do not remove this.
10. **(Low) LOC estimates** come from the number of call sites and files listed. They are not calibrated
    against past PRs in this fork.

## 10. Probe results

Filled from the run of `issue241-front-end-probe.yml` on `scratch/issue-241-front-end-probe` (see §1).

PROBE_RESULTS
