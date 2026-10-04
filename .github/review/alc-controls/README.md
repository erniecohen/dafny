# Assembly isolation controls

Status: this repair is source-only, uncompiled and unexecuted. The prior prototype's first baseline invocation completed and collected its context, but the four-control gate stopped because its version-output expectation was wrong. This scratch fixture investigates issue 118's unresolved default-Boogie cost gate. It is not a compiler change, proof result, or replacement for the issue 30 work. No baseline package, query, seed, solver argument, or resource reply is changed.

## First bounded controls

`B3AlcGate.csproj` is a dependency-free net8.0 console application. It has no Dafny/Boogie references, project references, or NuGet packages. The first executable gate is exactly four serial calls, baseline then candidate, each with the two fixed fixtures in `control-fixtures.json`:

1. `MainWithWriters(..., ["--help"])`: exit 0 and output containing `The Dafny CLI enables working with Dafny`.
2. `MainWithWriters(..., ["__b3_alc_invalid_command_6f60f0eb__"])`: exit 1 and the explicit unsupported first-command diagnostic. The fixture must not name an existing file.

No source files, solver path, wrapper, verification flags, or seed overrides are passed to either control. A control failure stops the remaining controls. Each invocation has a 30-second safety cap and a 128 Ki-character output cap. Context collection has a ten-second polling cap; the outer CI process must have its own deadline because GC/finalizer waits and product callbacks can block. Only Linux hosts with at most eight visible processors are supported in this first prototype.

The new CLI's built-in `--version` uses the process entry assembly through its command-line middleware. Reflectively calling a driver inside an ALC does not replace `Assembly.GetEntryAssembly()`: it remains the harness assembly. Consequently, the prior baseline control printed the harness's `1.0.0+...` informational version even though its private `DafnyCore` was version `4.11.0.0`. Changing the harness version would conceal that distinction. The help control instead checks a fixed product description from `DafnyNewCli.RootCommand` without using version output as product identity.

Product identity is asserted independently. The private-loader audit requires exactly `DafnyCore, Version=4.11.0.0, Culture=neutral, PublicKeyToken=null` with a 4.11.0 informational version. Actual loaded-assembly ledger entries include that informational version and SHA-256 digest. The scratch script separately requires the baseline core's exact informational version `4.11.0+fcb2042d.review.a171069d` and digest `9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9`. The ordinary candidate build reports `4.11.0+<full source HEAD>`; that exact expected value and its pre-invocation package digest are compared against the actual loaded core on both candidate controls. This preserves the existing build identity rather than inventing a fork release version. Each core must come from its role's exact package path. The four controls have no proof ownership scope.

The later public scratch compiler job may build these source files using its existing pinned .NET SDK, then call the resulting executable with package directories that contain `Dafny.dll`, `Dafny.deps.json`, `DafnyDriver.dll`, and their dependency graph:

```sh
dotnet build B3AlcGate.csproj --configuration Release --output build/alc-prototype
timeout --kill-after=10s 180s dotnet build/alc-prototype/B3AlcGate.dll \
  --baseline build/baseline/dafny \
  --candidate build/candidate/dafny \
  --receipt build/alc-controls.json
```

These are future CI fixture commands, not commands run while preparing this prototype. The receipt directory must exist. Expected success requires all four controls, expected codes/diagnostics, no invocation/loader/cleanup failures, actual context collection, and no residual direct host children. A failed gate returns nonzero and retains a completed-invocation receipt where available. Failures before a receipt can be constructed return 2 and print their diagnostic. The root workflow must capture both the receipt and console streams, not interpret a missing receipt as success. Do not silently rerun controls with different flags or a repaired baseline.

## Loader and shared-framework invariant

Each call creates a fresh collectible `ProductContext`. Its `AssemblyDependencyResolver` is rooted in that role's exact `Dafny.dll`. The driver is loaded directly from the same role's directory. `EnterContextualReflection` covers entrypoint discovery, invocation and completion. The reflected contract is the public `Microsoft.Dafny.DafnyBackwardsCompatibleCli.MainWithWriters(TextWriter, TextWriter, TextReader, string[]) -> Task<int>`; only framework strings, writers, task and integer cross this boundary.

Runtime TPA names are the only sharing allowlist. Duplicate entries for the same exact runtime path are deduplicated; different paths with the same assembly name remain an error. A matching TPA dependency must resolve to the actual default-context runtime path. Every other managed dependency must load privately from its own package, or resolution throws. There is no broad `System.*`/`Microsoft.*` sharing rule and no null return enabling implicit default fallback. This keeps `System.CommandLine`, `System.Reactive`, Dafny, Boogie and the rest of their package graph separate. Package dependency symlinks and unmapped native dependencies are rejected; supporting them needs an explicit later audit.

The per-run ledger records the resolver entry DLL/dependency-manifest digests, requested/resolved identities, informational versions, context names, paths and SHA-256 hashes for actual private/shared loads, then audits every private assembly and checks for non-TPA default-context contamination. The default context's own harness assembly is the sole host exception. Source/compiled harness hashes, initial framework assemblies, working directory, culture, visible processor count and actual runtime description are recorded. Scratch receipts record runner paths; the gate converts those paths to role-relative labels while preserving digest and identity information.

The public `BigInteger.GetHashCode` values for 4294967295 and 4294967296, a constant string hash, and `HashCode.Combine` are recorded before and after each invocation. CoreLib and Numerics identities/paths/digests must remain equal. No seed or hash implementation is mutated. This holds their process-global framework seed fixed across the contexts; it does not prove that all product state is isolated or that queries/resource counts match.

## Cleanup argument and its limits

The no-inline synchronous per-run frame contains all Assembly, Type, MethodInfo, Task, private Exception and context references. Private exceptions are reduced to bounded strings inside that frame. It reflects the private context's public `DafnyMain.LargeThreadScheduler`, calls `IDisposable.Dispose`, audits the loader and requests `Unload`. Only framework receipts and a long weak reference (`trackResurrection: true`) leave the frame. Bounded GC/finalizer cycles must make that weak reference dead before another invocation starts. A failure poisons the host for later calls. Simultaneous invocations are rejected.

Reading `LargeThreadScheduler` may initialize `DafnyMain` even when the help path never used it. Its initializer creates one 256 MiB virtual-stack background thread per visible processor; this is why the first host is bounded to eight processors. Disposal immediately follows that post-invocation initialization. Boogie's scheduler disposal cancels waits and interrupts threads but exposes no join. This prototype treats disposal exceptions as failure and requires actual ALC collection; it does not equate calling Dispose/Unload with completed teardown.

The Linux child audit reads all current host task `/proc/.../children` entries before/after each invocation and requires an empty baseline and empty final direct-child set. It does not claim to identify orphaned descendants after reparenting. That limitation is acceptable only for the two non-verifying argument paths; it must not be used as proof-work ownership evidence. Help/malformed controls create no configured solver process or wrapper.

The current CLI verification path creates `CliCompilation`/`ExecutionEngine` without exposing or disposing the engine on return. Solver instances subscribe to the shared Console.CancelKeyPress event; live solver/event references can pin a context. Scheduler disposal is also insufficient when private default-thread-pool work remains active. A timed-out invocation poisons the host and may require the outer CI supervisor to terminate the entire harness process. These are measured-control risks, not solved cleanup claims.

## Verification API prepared, deliberately unavailable from the CLI

`Runs.RunVerification(Product, string[], IProofLifecycleSupervisor?, TimeSpan)` can invoke the same unchanged public verification entrypoint. It rejects a null supervisor before constructing a product context, requires one explicit `--solver-path PATH` selecting that supervisor's existing absolute wrapper, and requires a positive invocation safety cap. A source-only `NativeProofSupervisor` implementation now exists; its cgroup/pidfd/stream controls have not been compiled or executed. See [the lifecycle plan](LIFECYCLE.md). Verification is not a command-line mode, and the four fixture controls cannot select it.

The future supervisor must own every run-specific transparent solver process group, forward stdin/stdout bytes unchanged, separately drain/log stderr, record executable identity and every created PID/descendant, and account for solver exits. It must preserve original options, queries, reset order, polling, resource-history and results. It may close/terminate run-owned groups only after CLI completion and log flush, or after recording a timeout/failure. `StopAndAssertNoOwnedSolvers` must return a complete nonempty ownership receipt with zero live owned descendants; absence of that receipt fails the run. Context collection remains mandatory afterward. Simply implementing an interface that reports zero is not sufficient evidence.

The next instrumentation phase must prove: wrapper transparency against captured actual commands; bounded EOF/error/cancellation handling; truthful ownership on early exit/failure; cleanup of the framework event subscribers; no live owned descendants after reparenting; weak-reference unload after a real verification; unchanged Boogie and solver hashes; immutable source/project manifests; complete JSON result/actual SMT reply denominators; and exact native query/resource comparison. Plugins (`Assembly.LoadFrom`), unmanaged global state, hidden project settings and unexpected default assembly loads need an explicit review before expanding supported workloads.

Only after those lifecycle controls pass should the small native-number baseline/baseline/candidate/baseline comparison be implemented. The full 696-batch focused matrix and broader P1 gate are intentionally absent. Four successful argument controls would establish only loader/argument-path cleanup for that executed toolchain, never native verification correctness or default cost parity.

## Static source manifest

`source-manifest.json` lists SHA-256 hashes of the prototype source/project/fixtures/README and the source commits reviewed for the entrypoint/cleanup contracts. It excludes itself and uses relative source paths. Recompute the manifest after edits, freeze exact sources before compilation, and preserve its digest in any later receipt. It is source identity, not a build or execution attestation.

## Native supervisor source stage

`NativeProofSupervisor.cs`, `native-solver-wrapper.py` and the three `proof-fixtures` prepare a strictly owned Linux native path. Proof CLI mode remains unavailable. The source requires a clean delegated cgroup, PID start-time/pidfd ownership and complete byte-forwarding/zero-populated receipts, followed by actual ALC unload. No native proof control or query/RU comparison has run; [LIFECYCLE.md](LIFECYCLE.md) lists prerequisite controls and the remaining limits.
