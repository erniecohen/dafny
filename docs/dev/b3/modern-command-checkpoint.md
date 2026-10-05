# Modern command source checkpoint (issue 126)

This checkpoint implements the reviewed [modern command plan](modern-command-plan.md) from exact integrated parent `51d527e0aa4fd428f4d42c881496f9d32dcb6001`. It is source-only: no compiler, test, proof, worker, target runtime or default compatibility gate has executed for this change. The source-derived control census is [modern-command-defined-controls.json](modern-command-defined-controls.json); its compiled TRX denominator remains null. The original 67 planning scenarios remain prospective design identities, not an executed denominator.

## Continuation and ownership

Only explicit modern B3 dispatch uses the new continuation. Default and explicit Boogie dispatch call the existing synchronous driver directly. Translate and legacy compilation retain their previous capability rejection. The native verification event lifecycle and existing options-based verify handler/consumers are unchanged.

`PreparedCliInputs` admits files through the existing engine-free target/file classification helpers and owns readonly copies of roots, foreign files and structured admission diagnostics. The B3 caller supplies one batch reporter across source, source-library and standard-library admission. Helper success cannot suppress a reported error or omitted requested dependency. Reporter failure, warning-as-error and disallowed warnings reject before parent construction; allowed diagnostics reach the owned subscriber once before Start. Fatal presentation uses the selected plain-text or JSON reporter. The optional reporter's absent branch preserves the legacy helper's behavior.

`Compilation` synchronously copies nonnull prepared roots in its constructor before asynchronous scheduling. It retains the original `DetermineRootFiles` branch when roots are absent. Prepared roots therefore do not reread project/library/stdin admission or depend on a caller list mutated before Start. Parser and resolver remain the existing pipeline, and compiler continuation receives the same resolved Program object rather than parsing again.

The B3-only module preparation API awaits successful resolution with a nonnull eligible scope, performs the original shared pre-VC translation, rejects reported translation errors and freezes the returned checking inventory before consumption. The ledger records every admitted module as not-started, preparing, completed or failed. Checking identities are globally unique, source owners belong to the admitted module/scope, and result bindings must remain identical until module release. Empty yields cannot replace missing preparation or resolution. Module-local task/normalized AST closures are released while the finite identity set/count/digest remains bound to the receipt.

Every nonempty unit requires `IsVerified` and normal stream completion through `RequireCompletion`; duplicate/missing terminal values, a later event, source fault, truncation and cancellation reject. All original non-success outcomes, including bounded results, reject compilation. Rejected receipts never invoke target hooks. Successfully prepared zero-unit scopes and ordinary Disabled scope are distinct; the CLI reports respectively that no assertion was proved or no proof was attempted. Existing trusted `.doo` inputs, source-library exclusions and declaration attributes stay current-policy exclusions rather than new B3 proofs.

The prepared reporting route completes its diagnostics-only view on error so the legacy default throwing observer cannot interrupt error publication. Summary, proof-dependency and log consumers retain the original error stream; all three tasks are awaited before returning. Their original helper bodies and the original native handler remain unchanged. The direct consumer control observes all three tasks faulting with the exact original exception. A separate terminal-then-fault orchestration control observes stream disposal, backend cleanup, a completed caller task and zero compiler calls.

The owned backend/compilation are cancelled and released before target hooks. The diagnostic subscription remains live until compiler reporting is complete. Finally disposes owned subscriptions/logger state and closes XML output. Caller cancellation is checked again before target invocation and after its returned result. Existing target APIs and process handling remain unchanged; this introduces no target process containment or comprehensive target cancellation claim.

## Dependencies and library information

Source project dependencies retain effective copied options and the existing trust/no-verify branch. Enabled selected B3 dependencies use the modern dispatcher, inherit cancellation and cannot silently invoke Boogie. Failure does not provide an accepted DooPath to the parent. Library child commands forward explicit B3 selection, explicit worker, effective solver path, supported time/resource/core/arithmetic values and ordinary skip. Program arguments remain after `--`; arbitrary internal Boogie/solver strings are not converted into CLI flags. The old default child argument construction is unchanged.

The library compiler still writes the existing parsed clone through the unchanged `LibraryBackend`, `DooFile` and `TranslationRecord` code. B3 alone records adjacent `.b3-verification.json` information after successful target completion. It captures the completed or Disabled receipt before target mutations, per-file admission policy and the actual produced archive byte count/SHA256. Its per-file flags describe file policy, not every declaration in that file. The sidecar explicitly grants no library trust and contains no observed solver version or end-to-end proof certificate. Existing readers do not consume it, change trust from its existence, or change `.doo`/`.dtr` version/serialization. Missing or stale information cannot establish verification. Ordinary Boogie output creates no new sidecar.

## Defined controls and evidence limits

The static census defines 95 xUnit rows across 33 methods: 85 Driver rows and 10 Core rows. It records complete class/method/argument/parameter identities and source-derived display names. The arguments require no long-string display truncation. Neither these names nor their expected assertions are observed TRX evidence.

| source class | defined rows | scope |
|---|---:|---|
| ModernB3ReceiptTest | 36 | in-process inventory/continuation relation; no worker/solver |
| ModernB3CompilationTest | 23 | fake backend and target witnesses; same AST, stream faults, cancellation and cleanup |
| ModernB3StreamTest | 7 | terminal/stream relation over in-process work items |
| ModernB3AdmissionTest | 11 | real input admission and diagnostic policy; no proof execution |
| ModernB3CommandBoundaryTest | 6 | actual skipped library CLI, selected dependency/configuration rejection and project/CLI precedence |
| ModernB3ProvenanceTest | 2 | informational metadata writer over inert bytes; not a valid proved library |
| ModernB3InputAndChildTest | 10 | constructor ownership, missing scope/resolution and finite child arguments |

The orchestration backend deliberately disables real package/solver preparation and produces named stub results. Those rows test pipeline ordering and rejection, not proof truth or generated-code B3 acceptance. The actual skipped library CLI row exercises ordinary format1.0 output and Disabled information with missing worker/solver paths. Actual project recursion uses a non-executable identity-only file and unsupported B3 arithmetic configuration; no process may execute that file. The opposite project/CLI override exercises the existing native hidden-skip library path, without an added B3 sidecar.

The issue126 integration regression retains eight CLI rows: three enabled modern B3 unsupported-configuration failures, three ordinary skip build/run/test rows with missing worker and solver paths, and the two unchanged translate capability failures. The test source is separate from the main source so its synthesized runner cannot conflict with Main. These are unexecuted direct CLI controls; none is a successful genuine B3 worker/solver verification case. Genuine enabled positive/negative modern command cases require separately admitted current CLI and exact worker/library/solver closure. Existing corpus expectations and full native/default verdict/resource gates remain unchanged and pending for this new source.

No normalizer, protocol, worker, vendor, semantic primitive or source/model proof changes are included. Current SDK/raw-assets evidence and execution prerequisites remain independent; this checkpoint does not satisfy them. Existing library receipts cannot stand in for current command acceptance.
