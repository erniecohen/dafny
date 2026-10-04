# Proposed restoration of six fixed shared-Boogie native smoke calls

This is a revised English implementation proposal only. Exact source
`71fc9dcff677f80d43ef3cb7c767806513c438a4` remains frozen on the published
`codex/b3-unavailable-metadata-controls` and
`scratch/codex-b3-native/unavailable-arity0` branches. No proof API is enabled,
no restoration source has been implemented or published, and no native proof or
solver has run under this proposal.

Inspected public [run 37238572256](https://github.com/erniecohen/dafny/actions/runs/37238572256)
passed exactly three fixed disposable nonproof loader controls. The actual
qualification pins are recorded below and in `QUALIFICATION-PINS.json`.
Both the coordinator's strict inspectors and independent exported-byte/detached-
receipt inspection passed. That satisfies the nonproof prerequisite only:
the first native MainWithWriters call with this common-library design has not run.
Earlier failed runs, including the private-plugin InvalidCastException smoke and
the unspecified-arity Reactive setup failure, remain failed.

Source implementation requires separate review of this concrete updated plan.
Any subsequent source checkpoint must then receive its own source review before
a compile/control dispatch. This proposal authorizes neither runnable proof
source nor a workflow run by itself.

The proposed acceptance scope is precisely these six calls, serially in one
shared-framework process:

1. baseline/true
2. candidate/true
3. baseline/reachable-false
4. candidate/reachable-false
5. baseline/fuel
6. candidate/fuel

The ordinary native parser, translator, Boogie implementation, outgoing SMT bytes,
raw resource replies and framework hash seeds are not patched or rewritten.
The wrapper changes executable launch identity and owns byte forwarding; it is
qualified for the exact pinned image and fixed invocation modes below. Six passing
calls would establish this smoke scope only. Query/resource parity, fresh Boogie
state, repeated/order-independent behavior, the 174-check denominator and the full
suite would remain unaccepted.

## Availability and shared-state contract

Keep the existing weaker metadata contract. Inventory every bounded common
AssemblyRef, but report `completeMetadataAvailability=false`. Exactly one edge is
unavailable: owner System.Reactive SHA256
`19f0112cd1f5172ee2688e96ddd44ada39a4bb1cb2315a154b63e9064f6e3dc0`,
requested identity `System.Runtime.InteropServices.WindowsRuntime, Version=4.0.0.0,
Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a`, metadata flags zero, and the scoped
EventRegistrationToken TypeRef. Both ordinary package resolvers, both complete
managed-file catalogs and the actual TPA catalog must prove that simple name absent.
The unavailable edge has no resolved object, target identity or Assembly slot.
No other missing edge is admitted and no package is supplemented.

The unchanged Default and private loader denials record actual demand under the
shared scope lock, poison permanently, and throw before fallback. The proposal
requires zero demands and no poison for every proof call and final acceptance,
even when ordinary Dafny catches a loader exception and produces an apparent
successful exit. A denied request in a proof is failure, not an expected control.
Unknown origin/context loads also poison through the runtime event ledger and
boundary audits; transient unknown loads cannot vanish from acceptance evidence.

The exact 13 Boogie roots and recursively classified common closure remain one
set of persistent Default Assembly objects. Same resolver selections, metadata
identities, byte hashes, paths and object slots are required throughout. All Dafny
and all other nonshared product dependencies remain private and collectible.
Framework reference-version compatibility is the existing binding rule; actual
resolved TPA identities, canonical paths, byte hashes and Assembly objects are
frozen. This is not an arbitrary identity allowlist or a framework upgrade.

Boogie static caches, counters and naming state persist. They are neither reset
nor described as fresh. The common receipt continues to declare
`persistentBoogieState=true`, `freshBoogieStateEstablished=false`, and
`nativeQueryOrResourceParityEstablished=false`, with repeat, interleaved and
reversed-order controls mandatory later. Same framework seed witnesses address
one source of comparison noise; they do not establish identical queries or RU.

## Exact prerequisite binding before any product load

The new proof harness must validate the following captured evidence and bytes
before common preload, creation of a private Dafny context or native invocation.
Missing evidence must fail closed; an approval boolean is not evidence.

- Bind the actually accepted nonproof qualification before any product load:
  public run `37238572256`, exact source
  `71fc9dcff677f80d43ef3cb7c767806513c438a4`, artifact `11316129057`
  named `b3-native-compile`, ZIP length `80709394` bytes, ZIP SHA256
  `4f4b07ddac0ca8d5d8876a349ce6871d12d0dcf774c757971dabe29bf8ce7a29`,
  and actual summary SHA256
  `74950dca1707fc774add3ee067ea4939ec76dfeb16c1edb2fa1f5af894b8688b`.
  Parse captured hash-checked bytes with bounded JSON depth/file sizes and safe
  nonsymlink archive extraction; a job exit or supplied approval boolean is not
  authority. Require exactly these three names in this order, with their exact
  fresh receipt hashes:

  | Control | Schema-2 receipt SHA256 |
  | --- | --- |
  | default-unavailable-demand | `25205d9db1cf088d937a374ba8044725cfc82d3f886ccb3de9a009375f24b0b1` |
  | private-unavailable-demand | `aca632876dbb7b9b05e5625b122d93d5982c4d40954bc3513673cabbf1d7cadc` |
  | reactive-event-unavailable-demand | `09ddb7180ba82ce709cffc64da49c7fa62d872226cca0bfb6256e0493cf8546b` |

  Independently require each exact strict denial chain, actual trigger=true,
  one exact poisoned unavailable demand and expected route. Default/private
  each have complete two-node FileNotFoundException chains totaling 520 UTF-8
  bytes. Reactive has a complete three-node TargetInvocationException /
  FileNotFoundException / FileNotFoundException chain totaling 620 UTF-8 bytes.
  Its selected method is public/static/nongeneric, exact metadata token
  `0x06000771`, owner SHA `19f0112c...`, exact manifest module and
  `System.Type`, `System.String` parameters; the actual API was invoked
  before the expected denial. The selected-method record alone is insufficient.
  Terminal FileNotFound type, exact denial message and exact requested FileName
  remain mandatory. No incomplete chain, unknown exception or unrelated error
  may stand for denial.

  Each of the three fixed control process stages records one fresh owned root,
  exit zero, no observed descendant, no supervisor poison or cleanup signal,
  root exit observed/reaped, and no remaining coordinator children. Private
  weak collection and Default-only final contexts are actually recorded. All
  seven stages require exit zero, no poison/failure/signal and clean final
  ownership; unlike the three control hosts, the SDK build stage permits and
  records its owned compiler descendants. Require all ten exact
  immutable boundaries, including `final`; the build has zero warnings/errors.
  These are the fixed nonproof facts, not evidence of a solver or proof engine
  lifetime. Revalidate the summary/individual receipt linkage and input hashes.

- Preserve the qualified source38 manifest
  `4230adf2ba624d42574d59ed95d4af1e28346d3260ffb79bf4249f1c80097a05`
  and coordinator5 manifest
  `383c8bffa310bf0ede4322e8679fa7026cf3e3733504404ee987d08f1d262b81`.
  The qualification's actual built bundle pins are:

  | File | SHA256 | Bytes |
  | --- | --- | ---: |
  | B3AlcGate.dll | `f769dc26690a4175706a9758332b56438c4da32e690b3ed09fcef342e4678139` | 385024 |
  | B3AlcGate.deps.json | `3f19663789fcabc493fe5e88617cc68bdcb406e03c6aade5e36accc22b090107` | 397 |
  | B3AlcGate.runtimeconfig.json | `97c9700542b659150b230c3578b29530fd76ab01ec66a92cd16945e0245713df` | 328 |
  | source-manifest.json | `4230adf2ba624d42574d59ed95d4af1e28346d3260ffb79bf4249f1c80097a05` | 11378 |

  Source38/coordinator5 bytes were rechecked against frozen reviewed source;
  all four exported built files and 329+335 product files were independently
  rehashed. The run's dotnet executable SHA is
  `7dda7a639bc35fba05c40d308b0296450aa4e628d109245a6312c385da9b0931`
  (68424 bytes), runtime is .NET 8.0.31, CoreLib SHA is
  `1f6ae40a2fa4e568b047960527056c5ff771dc3286c419f0a1082adb6e915740`,
  and Runtime.Numerics SHA is
  `7b0c03fb1860a635ed4277812f147bcfd44c5d0fda7300f573ad78a4eb67b202`.
  Require the complete recorded 168-entry TPA identity/path/hash/byte catalog
  in the new runtime preflight, not just these two examples. Those runtime
  binaries were not exported in the qualification artifact: inspection
  cross-checked their on-runner receipt/boundary facts, without substituting
  manufactured local rehashes. The new proof host must hash its actual runtime
  bytes and bind its own Assembly object slots. Object slots and randomized
  seed witnesses from distinct qualification hosts must not be compared as
  though they represented one process; all six new proof calls must share the
  new host's actual unchanged witness and Assembly objects.

  The new proof harness must have a distinct source manifest and actual assembly
  hash. Never relabel `f769dc...` or source4230 as a proof-enabled build.
  Bind byte-identical common/private resolvers, deniers, unavailable metadata
  evidence, fixed negative controls and their schema-2 inspector to source71fc.
  The prior nonproof qualification does not automatically qualify a changed
  smoke adapter or new helper. Failed source/gate manifests remain historical
  failed evidence and are not accepted as replacement qualification.

- Keep the original baseline public run 37182760834 archive SHA256
  `679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae`,
  declared source `b07c038737d6713b6d1a5848d7568bdc972de7dd`, Core SHA256
  `9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9`,
  and actual informational version `4.11.0+fcb2042d.review.a171069d`.
  Keep exact archived candidate public run 37221204349/source
  `a6132c6758c863ec4a897e4ac1534dc727f0b006`, complete package manifest
  `3d592286e3a08229a3b4fca3a987a05725482604968d1c1d4709fce6496f1c96`,
  Core SHA256 `92824cdc6b5247f7bc2418c78aa55dafe9c2a8f2572c9256e37daea8ea25a6d8`
  and version `4.11.0+a6132c6758c863ec4a897e4ac1534dc727f0b006`.
  Both full inventories, deps/runtimeconfig and resolver-selected assets are
  checked before and after each call. Earlier failed smoke evidence stays failed;
  a successful archived candidate compilation establishes package provenance only.
- Bind actual Linux x64 runtime/TPA inventory, harness, dotnet, deps/runtimeconfig,
  framework witness and original/source fixture bytes. Initial host has only
  the reviewed harness/framework in Default, only Default ALC, and no children.
- Keep exact Z3 5.1.0 image SHA256
  `b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23`.
  Recheck pinned origin evidence `a64314c9ee010e48e19bdf9ede2ae6260b2dfbd857540a9b1c06010c5e6638af`,
  actual ELF assertions and exact three source files from Z3 tag commit
  `0b6cdcdbc65da25ef0f73ac9da210574d0f66cf8`. Permitted executable argument
  arrays remain exactly `[-version]` and `[-smt2,-in]`. Absent origin evidence or
  altered actual image/dependencies is a preflight failure. Origin qualification
  applies only to this sealed native image and stdin frontend, not arbitrary ELFs.
- Preserve inspected public eleven-control lifecycle run 37215797270/source
  `875e97e70198459993803a3ac5f41db703eb8938`, receipt
  `60992bd42a47fbc9e3fca7294253d182947dd8ddfd4977d76361818bac2576bd`,
  old source manifest `e25b41f0d0fb6c29708d64cccc5c4b945bd64f177054156db667688243cf9a42`,
  and old assembly `ec2e3910a0cdc4871a1c1c52b540c6e302b09fd83d1e47e509ab1d91855db680`.
  All original19 source bytes remain unchanged, particularly supervisor and
  wrapper. Match actual lifecycle implementation components to that receipt,
  keeping its UID0 Linux x64 fixed-control limitation explicit.

## Minimal separately reviewed restoration

Use a fresh isolated source draft based exactly on source71fc after plan approval.
The two disabled adapters proposed for restoration are outside the original19
lifecycle inventory:

| File | Qualified source71fc SHA256 | Proposed change |
| --- | --- | --- |
| NativeProofSmokeControls.cs | `963abe3c09fd452b16c3873fdbe85fdaa87111ce9b0a0c6cf1d0aa475f2ad15f` | Keep fixed packages/fixtures/budgets; restore only a qualified single-use six-call session and success/failure finalization. |
| SharedNativeRuns.cs | `8359809efe509ae9fc44c226dc822c0a7835d9a1b1c1b29555d43fd3516a80ca` | Admit only the session's exact one-use case permit; restore ordinary invocation/cleanup/collection with fail-closed scope checks. |

Their retained native bodies compiled in nonproof qualification, but were never
called and have no runtime proof qualification. New prerequisite/session helper,
strict smoke-inspector and dedicated coordinator files may be added; all receive
a complete source diff and new byte pins. `NativeProofSmokeProgram.cs` stays
byte-identical (SHA `89c0d44a8504efdc5e8e6229845ef192b4df007d7755fb86a94d488f70df0d31`);
its existing fixed-input route can reach only the newly qualified fixed Run.
No ordinary native CLI or arbitrary caller arguments are exposed.

Preserve all original19 byte-for-byte, not just selected implementation components:
`.gitattributes`, `B3AlcGate.csproj`, `Isolation.cs`, `LIFECYCLE-CONTROLS.md`,
`LIFECYCLE.md`, `NativeLifecycleControls.cs`, `NativeProofSupervisor.cs`,
`Program.cs`, `README.md`, `Run.cs`, `control-fixtures.json`,
`lifecycle-control-fixtures.json`, `lifecycle-seal-probe.py`,
`native-lifecycle-fixture.c`, `native-proof-fixtures.json`,
`native-solver-wrapper.py`, `proof-fixtures/false.dfy`,
`proof-fixtures/fuel.dfy`, and `proof-fixtures/true.dfy`.
The build uses a command-line StartupObject override; no original csproj or
Program route changes. Ordinary Program's proof mode remains disabled.

Keep all other source71fc payload bytes unchanged, including SharedCommonLibraries
(e065859d...), SharedProductContext (e5a2202f...), NativeProofEvidence
(59c4a162...), NativeElfOrigin, nonproof controls, schema-2 inspector and technical
origin evidence. Do not alter product packages, source, framework/Boogie state,
denials, supervised byte forwarding or queries.

The fixed NativeProofSmokeProgram can retain its strict two arguments
`--inputs ABSOLUTE_JSON_PATH`, unmapped-field rejection and bounded input JSON.
Extend smoke input with a required qualification pin for proof mode (an optional
nullable record parameter may preserve construction by the nonproof metadata
helper, which never invokes proof). Proof Run must reject null, unknown or
mismatched qualification before product/common load. No JSON Dafny arguments,
case order, sources, budgets, backend selector, prover factory, seed or native
solver flag can be accepted from the caller.

Restore NativeProofSmokeControls.Run only to the reviewed six-case implementation,
after that qualification preflight. Restore SharedNativeRuns.RunVerification only
for calls admitted by this fixed session, with exact product/fixture/argument
matching, configured wrapper and serial one-use-per-case admission. A sealed
session/permit issued only after validated exact prerequisite receipts binds the
ordered call to its complete product pin, fixture hash, exact full argument array,
wrapper/owned paths, one shared scope and next ordinal. A permit is consumed once
under a serial admission lock; out-of-order, duplicate, concurrent or mismatched
calls permanently poison and end the session. The public internal adapter cannot
accept a mere caller-supplied qualification boolean or unvalidated arguments.
There is no ordinary proof CLI route and no general-purpose fallback when a prerequisite fails.

Use the existing AssertAcceptable immediately after preload and before the first
private context, before every case admission and again before actual native invocation,
after CLI/cleanup/collection and after evidence inspection. Any exception, denial,
loader audit, timeout, ownership fault or collection failure poisons both the fixed
session and shared scope and ends the host. Success finalization must use existing
CloseAndSnapshot so final audit,
snapshot and owned-callback retirement share the scope lock. The final receipt
must independently require zero demands, no poison/failures, validated complete
runtime ledger, exact context set and all six passing cases. A caught exception,
an earlier acceptable snapshot or a green exit cannot erase final poison. A late
captured owned resolver/event callback still terminates with failure 126; the
outer coordinator requires actual host exit zero before inspecting acceptance.

## Ordinary native call reachability

Use the same public MainWithWriters(TextWriter,TextWriter,TextReader,string[])
returning the framework Task<int>, inside the owned private context's contextual
reflection scope. The source path is ordinary: MainWithWriters schedules
ThreadMain; `verify` selects the modern parser; DafnyNewCli's registered handler
calls VerifyCommand.HandleVerification; CliCompilation selects the ordinary
native Boogie backend when no experimental selector is supplied. The harness
neither creates substitute DafnyOptions nor injects TheProverFactory.

Boogie's unchanged ProverFactory.Load derives its SMTLib path from its executing
assembly directory and calls Assembly.LoadFrom. Its exact common Boogie assembly
already resides at the baseline canonical path in Default; the pinned SMTLib
assembly and factory dependencies are the same Default objects. This removes the
previous private/Default cast mismatch by supported assembly sharing. It remains
a reachability design until actual native invocation succeeds. No claim is made
that Reactive's optional WinRT surface is globally unreachable: the positive
six-call path must itself exhibit zero runtime demand and actual SMT/proof output.

Each pair uses the same exact absolute fixture path. Record the entire argument
array: verify, fixture, cores1, verification-time-limit20, resource-limit200000,
solver-path the owned transparent wrapper, and actual JSON/CSV log destinations.
The two log arguments retain exact
`json;LogFileName=ABSOLUTE_PATH` and `csv;LogFileName=ABSOLUTE_PATH` syntax.
The 20-second/200,000-RU limits are explicit smoke budgets, not the later full
suite's unchanged defaults. A 60-second invocation cap is safety only. Native
solver flags/options and generated queries remain those of the pinned ordinary
packages; the wrapper forwards raw byte streams without edits. Actual per-launch
arguments and all emitted SMT options are retained in receipts.

## Privilege, ownership and actual collection

Use the qualified Linux x64, cgroup-v2, executable memfd and pidfd path. Proof
invocation requires actual effective UID0 in fixed CI, a fresh private delegated
parent, pids.max300 and the required fd-relative permission checks. Coordinator
remains outside that parent. Only the fixed proof host is launched through the
reviewed sudo path with resolved absolute dotnet and a credential-free environment;
no unprivileged deployment claim, shared ancestor configuration change or owner
host execution is implied.

Every native launch remains owned by an exclusive leaf, verified PID/start-time
and pidfds, token handshake, host process-lifetime pidfd watcher, sealed captured
solver image and hash-bound stream/completion ledger. Ordinary CLI output and
logs finish before admission closes and cleanup checks the exact leaf empty and
removed. A fallback cgroup kill or outer parent drain preserves failure for proof
smoke even when it leaves zero members. No unowned or numeric-PID signal is used.
The outer coordinator retains its anchored bounded exclusive-parent cleanup on
host failure/cancellation and authoritatively checks procs/events before removal.
No broad exception can satisfy an expected proof result.

Dispose the actual private LargeThreadScheduler, then leave the no-inline frame
with only detached values/strings and a long WeakReference. Retire/unload the
private context and require actual collection within ten seconds before the next
call. Actual ExecutionEngine disposal is not asserted where no engine handle is
owned. Shared statics/tasks/callbacks retaining private objects cause a surviving
weak reference and failure; no Console event-field surgery or static reset is
permitted. Before/loader/collection/evidence audits yield exactly 28 successful
Default snapshots for six runs, within the existing bound32. Complete common
objects and prior framework objects remain stable across all snapshots.

## Six strict denominators and evidence

True uses `lemma NativeTrue(x:int) { assert x+1 > x; }`; reachable false contains
an actual reachable failing assertion; Fuel uses the exact pinned recursive
fixture. Their unchanged bytes are pinned as follows:

| Fixture | SHA256 | Expected native CLI exit |
| --- | --- | ---: |
| proof-fixtures/true.dfy | `bf2c7f7498a9221291dbc5244c3725f0fb9af93b741638595bdc11f3b83d24e6` | 0 |
| proof-fixtures/false.dfy | `a50e9615c8b49eefe55a458f6f4df613f11164406074b9a011f7f389aad2dc8e` | 4 |
| proof-fixtures/fuel.dfy | `55a2d36861839f955e222ab8b2ae6f838645ef013e8610b588f391bbe61c3206` | 0 |

The Fuel fixture is specifically the existing Down function and NativeFuel lemma
with its explicit fuel attributes and Down(0) assertion; it does not cover the full
Fuel suite or every fuel behavior. Require
nonzero actual JSON proof batches, target routine/source/assertion coordinates,
strict Valid for true/Fuel and actual Invalid plus `assertion might not hold` for
reachable false. Unknown, timeout, resource exhaustion, skipped/zero-batch or
mere version launches cannot satisfy a control.

Use unchanged NativeProofEvidence. Require actual proof check commands/replies,
nonzero ProofVcGroupCount, JSON/CSV agreement and complete launch stream hashes.
For every actual reset/VC group, the final raw :rlimit/outcome pair must match the
untouched JSON batch multiset as the pinned native resource flow specifies.
No cumulative difference, synthetic cost, resource adjustment, query canonicalizer,
seed mutation or normalized option set is allowed. Preserve every raw prefix,
blocking clause, resource reply and per-launch option sequence. A reported total
is a descriptive sum, never a substitute for exact per-group evidence.

The new strict inspector must validate the full common closure, unavailable edge,
zero-demand/poison state, complete runtime ledger, all28 object/context audits,
exact6 names/order/verdicts/denominators, effective UID, inputs/fixtures/argument
arrays, cleanup hashes, true weak collection and distinct actual harness/source
pins. Require all original source71fc qualification pins and unchanged19 component
digests before proof preparation, and recheck these captured prerequisites and
all fixtures/packages before/after every case. The outer coordinator validates
proof host exit zero, each exact cleanup and group denominator, and its final
Default-only/zero-demand/no-poison receipt before calling the fixed six PASS.
Read-only validator imports execute captured hash-checked source bytes;
source/coordinator pins and dotnet/DLL/deps/runtimeconfig/compiled manifest are
rechecked before/after build and every control boundary, with initial exact source
inventory only. Final owned children must be empty; parent removal without cleanup
intervention and host exit zero are independent acceptance conditions.

## Stop points and later evidence

The three nonproof prerequisites now actually PASS on the pinned source71fc
receipts above. The next stop remains separate review of this English revision,
then a fresh source-only restoration draft and complete reviewed diff. A separately
authorized bounded scratch compile/control dispatch
may execute these six fixed calls. Any failed case stops, preserves its raw
receipts and leaves this scope NOT GREEN; neither weaker verdicts nor higher
smoke budgets are a repair. No default package/flag/query/cost change is justified
by this diagnostic gate.

Even six PASS leaves repeat, interleaved and reversed-order controls outstanding,
then an exact 174-native-check comparison and the unchanged full suite. Persistent
Boogie state may still change naming, query ordering or cost; no full backend,
default native parity, repository-green or fresh-engine claim follows from this
proposal or its possible smoke acceptance.

The archived a613 six calls would qualify only loader/native-smoke behavior of
that exact archived package. The eventual 174-check comparison, full suite and
standard-library/default parity gate must use the exact final integrated
candidate build/package and source. A successful a613 session cannot qualify
later integrated Real, bitvector, G3 or other backend changes. The unresolved
framework/HashCode/query-order/cost question and persistent shared-state risk
remain open even if all six smoke calls pass.
