# P0 semantic correspondence and call-site audit

This is a source audit, not a proof of an adapter. Dafny references below are to
baseline `81062e7df6eecd9d73a0671abd83206d13b67893`; Boogie references are to the
package/tag source `677b5f6cfd74207612865adf1ecc08887ae15953`, established by the
[provenance chain](README.md#boogie-provenance). B3 references are to the requested
`b24a2b2d6101df900e67d3bb151c4e85af4cd16d`. These are public sources.

## Existing integration seams; no call sites changed in P0

| Site | Current behavior | Requirement before changing it |
| --- | --- | --- |
| [BoogieGenerator.Translate](https://github.com/erniecohen/dafny/blob/81062e7df6eecd9d73a0671abd83206d13b67893/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1187) and `DoTranslation` | Validate cardinality, establish module scopes, translate with the shared prelude and proof dependencies. | Share this result; preserve the type/cardinality admission receipts, all modules and proof/source ownership. Do not create a second, weaker translator. |
| [DafnyProgramVerifier.GetVerificationTasksAsync](https://github.com/erniecohen/dafny/blob/81062e7df6eecd9d73a0671abd83206d13b67893/Source/DafnyLanguageServer/Language/DafnyProgramVerifier.cs#L28) | Translate a module, optionally print it, then call `engine.GetVerificationTasks`. This serves the modern CLI and IDE. | Select an alternative after typed translation and before Boogie task preparation; keep the native branch unchanged. Include the translation-cache branch in the boundary review. |
| [SynchronousCliCompilation.Translate](https://github.com/erniecohen/dafny/blob/81062e7df6eecd9d73a0671abd83206d13b67893/Source/DafnyDriver/Legacy/SynchronousCliCompilation.cs#L430) and `BoogieAsync` | Legacy entry point translates and then verifies through Boogie. | Cover or explicitly reject legacy consumers. No hidden fallback. |
| [ExecutionEngine.GetVerificationTasks](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/ExecutionEngine/ExecutionEngine.cs#L853) | Resolve, typecheck and prepare native Boogie tasks; may monomorphize for `:m`. | B3 may reuse admission checks, but must not invoke this native preparation/VC/prover path. Do not assume the `:a` translator result was already monomorphized. |
| [IProgramVerifier](https://github.com/erniecohen/dafny/blob/81062e7df6eecd9d73a0671abd83206d13b67893/Source/DafnyCore/Pipeline/IProgramVerifier.cs#L23), CLI compilation and IDE consumers | Native verification-task/result and proof-dependency reporting. | P1 needs complete task/obligation accounting, cancellation, diagnostics and explicit non-success outcomes across consumers. |
| [B3 ResolveAndTypeCheck](https://github.com/dafny-lang/b3/blob/b24a2b2d6101df900e67d3bb151c4e85af4cd16d/src/Driver/Main.dfy#L149) | Resolve raw AST, check types, then static consistency before verification. | A worker constructing `RawAst` must run these checks or implement an independently justified checked construction boundary. |
| [B3 driver and ProveAndReport](https://github.com/dafny-lang/b3/blob/b24a2b2d6101df900e67d3bb151c4e85af4cd16d/src/Verifier/Verifier.dfy#L511) | Errors are printed; no aggregate verification status is returned to the driver. | P2 must supply structured outcomes, fail-closed transport and nonempty completed-check accounting. Process exit zero proves nothing about verification success at this pin. |

## Structured statements and unstructured blocks

[Implementation's structured constructor](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/Core/AST/Implementation.cs#L390)
assigns `StructuredStmts` and immediately constructs `Blocks` through
`BigBlocksResolutionContext`. Therefore a **pre-VC** program already contains a
lowered CFG as well as its structured tree. The alternative blocks constructor
has no structured tree. An adapter cannot infer “unstructured source only” merely
from nonempty `Blocks`, and it cannot assume every implementation has `StructuredStmts`.

[BigBlocksResolutionContext](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/Core/AST/StructuredBoogie/BigBlocksResolutionContext.cs)
resolves labels, synthesizes branch blocks and lowers while bodies/invariants.
The structured printer selects `StructuredStmts` under its normal conditions;
`/printUnstructured`, instrumentation and inlining can select the block form.
Keeping both representations in the census is deliberate. A future normalizer
must choose one semantic source and account for nondeterministic guards, labels,
break/return, invariants and path attributes once. Translating both would duplicate
checks or assumptions. The mutable structured tree is not an immutable proof object.

## Free contracts, calls and old state

The [implementation precondition construction](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/ConditionGeneration.cs#L280)
assumes **both checked and free requires**. Its postcondition construction checks
only non-free ensures. This is the callee perspective.

The [call desugaring](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/Core/AST/Commands/CallCmd.cs#L815)
checks a requirement only when both `!req.Free` and `!IsFree`. Ordinary free
requirements are not automatically caller assumptions: `always_assume` and
stratified inlining are explicit exceptions. Wildcard inputs use an existential
precondition check and subsequent substitutions. After freezing inputs and old
modified globals, it havocs the modified frame and result temporaries, then assumes
**all** postconditions, free or checked, under current/old substitutions. Only then
are results assigned back. The distinction is observable; mapping `free` uniformly
to “assume everywhere” is wrong. These native semantics include unchecked premises;
a backend preserves their meaning, rather than claiming those premises were proved.

Dafny also distinguishes well-formedness, call and implementation procedures, and
uses free clauses for translator-established facts. Preserve those identities.
`OldExpr` denotes the relevant pre-state, not a syntactic replacement of every
identifier with a fresh name: bound variables, frozen inputs and heap state need
different treatment. Dafny's
[ExpressionTranslator](https://github.com/erniecohen/dafny/blob/81062e7df6eecd9d73a0671abd83206d13b67893/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L155)
also uses explicit labeled heaps. An adapter needs an explicit state/substitution
argument covering globals, locals, parameters, results and labels.

## Where-clause scheduling

[VerificationConditionGenerator](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/VerificationConditionGenerator.cs#L668)
injects global, formal and local where assumptions at implementation entry.
[GetParamWhereClauses](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/ConditionGeneration.cs#L370)
substitutes procedure formals into implementation formals and installs output where
clauses on implementation outputs for havoc. Local initialization has special
assumption-variable handling.

During [havoc passification](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/ConditionGeneration.cs#L1252),
**all** fresh incarnations are created first, then the affected variables' where
clauses are assumed with that updated substitution (and the old-frame substitution).
A simultaneous havoc with interdependent where predicates is therefore not a sequence
of single-variable havoc/assume pairs. State-command locals also receive their where
assumptions on entry. At calls, output-temporary where clauses are installed after
the full substitution map exists and before the frame/result havoc. Ordinary
assignment is not a generic authorization to re-assume a variable's where predicate.
Do not strengthen semantics by inserting where assumptions after every assignment.

## Raw maps and the repaired type encoding

Dafny defaults to Boogie's arguments encoding (`:a`), not monomorphic native arrays.
[UseArrayTheory](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/ExecutionEngine/CommandLineOptions.cs#L408)
requires monomorphic encoding and no explicit array axioms.
[Boogie2VCExpr](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCExpr/Boogie2VCExpr.cs#L1459)
translates raw equality to equality, while the `:a` map axiomatization explicitly has
no extensionality axiom. Consequently the pointwise equality of raw Boogie maps does
not generally authorize identifying them. Native SMT arrays, which are extensional,
would silently add a fact if substituted indiscriminately. Dafny's own `Map#Equal`,
`IMap#Equal` and collection extensionality are **separate prelude facts** whose guards,
domains, boxing and ownership must remain intact.

The pinned [same-index read-over-write repair](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L359)
restricts native-result payloads to values preserved by the box/unbox round trip.
The reverse built-in casts were already removed by the fork's earlier repair.
A representation specialization must model the repaired carriers/casts, including
Boolean cardinality and possibly empty types; it must not restore a surjective
reverse cast, unguarded raw store law, or native-map extensionality. The P0 census
is not evidence that such specialization is implemented or sound.

## Assertion publication and visibility

[Wlp.Subsumption](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/Wlp.cs#L145)
controls whether an assertion also restricts its continuation: mode 0 never,
mode 1 except quantifiers, mode 2 always; `{:subsumption ...}` overrides the global
setting. B3 distinguishes
[`Check` from `Assert`](https://github.com/dafny-lang/b3/blob/b24a2b2d6101df900e67d3bb151c4e85af4cd16d/src/Verifier/Verifier.dfy#L313):
`Check` proves and forgets, while `Assert` proves and extends the context. The adapter
must respect each assertion's effective publication policy, not translate every
assertion to the same B3 statement. Diagnostics, split check/free pieces and
post-check summaries also need correspondence controls.

[Pruner](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/Prune/Pruner.cs#L75)
traverses definition dependencies and stops a hideable function-to-axiom edge when
that function is hidden. Function and constant definition-axiom ownership are
explicit edges, not merely annotations in an axiom's expression.
[RevealedAnalysis](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/Source/VCGeneration/Prune/RevealedAnalysis.cs)
tracks hide/reveal and scope push/pop, merging availability across paths; it also
recognizes `AlwaysRevealed` and original polymorphic declarations. The prelude
functions are set always-revealed by Dafny's translator. Eagerly installing every
emitted axiom into one B3 context would destroy this boundary. B3's lazy
`Explains` axioms alone do not establish equivalent Dafny visibility semantics.

## Public-source correspondence checklist

All rows remain open **implementation obligations**. The source audit identifies
the requirements; it does not discharge them.

| Area | Required argument and controls | Campaign boundary |
| --- | --- | --- |
| Typed admission and specialization | Preserve type/cardinality validation, empty/nonempty and finite/infinite carriers, polymorphism, boxing and repaired map payload laws. Reject unsupported forms before proving. | P3/P4 |
| Declarations and expressions | Account for every census node/operator/builtin, map/lambda binder and trigger; distinguish native real/BV theory from custom uninterpreted literals. Positive, false and unsupported controls. | P4/P7 |
| State and contracts | Relate frozen input/old heaps, simultaneous havoc, where scheduling, free contracts and outputs to the same observable states and obligations. | P5 |
| Control flow | Relate structured/CFG choice, loops/invariants, nondeterministic branches, labels and exits; neither duplicate nor omit a checking unit. | P6 |
| Publication | Relate Boogie subsumption and checked/free splits to B3 Check/Assert and continuation contexts. | P3/P5 |
| Definition availability | Relate ownership, hide/reveal, always-revealed prelude declarations, scope joins and isolated axiom contexts. Negative hidden-definition and cross-task leak controls. | P8 |
| Results and lifecycle | Reject unknown, incomplete, unsupported, cancelled and tool-error outcomes; require completed nonempty accounting; prove source/result identity and no Boogie proving on the B3 path. | P1/P2/P9 |
| Assurance and release | Review the executing implementation separately from a soundness model; demonstrate default-Boogie compatibility and reproducible pinned packaging. | P10 and later assurance |

At this pin, B3's builtin type names are `bool`, `int` and `tag`
([Types.dfy](https://github.com/dafny-lang/b3/blob/b24a2b2d6101df900e67d3bb151c4e85af4cd16d/src/RawAst/Types.dfy)).
Its raw AST and checking pipeline are usable bootstrap components, but no direct
support for the whole Dafny prelude follows from compiling B3. No soundness model
from an unmerged pull request is incorporated or treated as a refinement theorem
for the executing verifier.
