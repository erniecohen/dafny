# Real corpus triage (issue 124)

This diagnostic preserves the existing acceptance requirements. Its receipt is
not a replacement full gate. Worker `Verified` with a frontend warning does not
satisfy a warning-free CLI success requirement. `Inconclusive`, including
`unknown`, does not satisfy a required `Failed` negative control. A higher
resource budget is exploratory evidence and cannot repair the original budget's
receipt.

## Mathematical and source correspondence

Let E embed mathematical integers into mathematical reals and let F be
mathematical floor. For every integer i, E(i) is integral, and F(E(i)) = i:
i is itself no greater than E(i), and the next greater integer's embedding is
strictly greater. Therefore E(F(E(i))) = E(i). This argument applies to negative
integers as well as nonnegative integers. It uses no rounding or truncation
convention. [SMT-LIB mixed integer/real theory](https://smt-lib.org/theories-Reals_Ints.shtml).

The source prelude declares native `Int(real): int` and `Real(int): real`
definitions. The audited normalizer substitutes them only through their actual
typed Body or active unconditional AlwaysRevealed defining axiom, with the exact
formal/binder identity checks described in [native-real-correspondence.md](native-real-correspondence.md).
For the source expression `((i as real) as int) == i`, Dafny first saves the real
operand o of the outer cast and emits the checked integrality obligation
`Real(Int(o)) == o`. It then emits the final asserted integer equality.
`ConvertExpression` supplies the two native conversion calls. The normalized
assignment and both original check identities must remain visible in the
diagnostic's typed BPL, normalized IR and check ledger. The integrality check may
be learned according to the original subsumption mode only after its actual
attempt; it must never be inserted as an unconditional assumption to lower the
price of the following check. No fixture is simplified for this diagnostic.
[Prelude](../../../Source/DafnyCore/DafnyPrelude.bpl#L605),
[conversion checks](../../../Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1478),
[conversion expression](../../../Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1279),
[builtin calls](../../../Source/DafnyCore/Verifier/BoogieGenerator.BoogieFactory.cs#L523).

The universal fixture's field equalities and `forall x: real :: x + 0.0 == x`
are valid in the mathematical real carrier. The empty-trigger frontend warning
is independent of that truth; this runner retains and reports it under the
original `allow-warnings=false` policy.

The irrational fixture's source precondition `x*x == 2.0` is satisfiable in that
carrier, so the reachable `assert false` must fail. An irrational square root of
two is permitted even though literal syntax stores rational constants. The
source precondition, generated ordinary context, real variable and original
false goal must remain unchanged in the emitted request. Returning unknown is
an unresolved operational discrepancy, not evidence of unsatisfiability or of
a rational-only interpretation. [SMT-LIB Real carrier](https://smt-lib.org/theories-Reals.shtml).

## Diagnostic boundary

The fixed three original source files are captured verbatim under
`.github/review/real-triage/original`, independently of any current corpus warning
annotation. They are read without edits, checked against the fixture manifest, parsed and resolved by the captured compiler, translated once, and
resolved/typechecked by the pinned Boogie package. The runner performs no Boogie
VC preprocessing, pruning or native prover call. It writes the complete emitted
typed module, original normalized program, source obligation ledger and static
check conditions before any worker request. It verifies that the ledger and
original checks have exactly the same identities.

The baseline uses the unchanged corpus configuration: 200,000 resource units,
20,000 milliseconds, arithmetic solver 2, solver arguments `-in -smt2`, and the
ordinary host package. Every additional caller-specified budget is strictly
greater, separately labeled exploratory, and changes only `resourceLimit` in
the request configuration. The same normalized program and program hash are
reused. Each request launches a fresh ordinary WorkerProcessClient and checks
the package fingerprint again. No flag, seed, source condition, learning mode,
check order or query rewrite is installed. The worker's existing solver options
remain in force, including its existing `auto_config=false` and `mbqi=false`.

Every completion, actual check attempt/reason/breadcrumb, check-coverage result
and strict expectation match is serialized. No unknown negative is waived.
These fixtures are straight-line or single assertion units; this diagnostic
requires complete coverage of their owned static goals for a strict match.
It does not impose that rule on arbitrary protocol programs with unreachable
goals. Actual proof-resource totals remain unavailable in this worker protocol;
a configured limit is not a measured resource count.

The exact prior receipt records two Verified universal units followed by the
frontend warning/exit 2; the conversion's checked integrality attempt reports
`canceled`, followed by a `push canceled` tool error; the irrational false goal
reports `incomplete (theory arithmetic)`. These are three distinct unresolved
acceptance outcomes, not changes to the mathematical expectations. The current
corpus may separately suppress only its universal quantifier warning through the
supported source `{:nowarn}` attribute; the captured original warning and its
strict mismatch remain in this diagnostic.

The source manifest and library must be the exact already verified a6 build:
source fingerprint
`a6ecb742096ddf2f4a6dbcfefb847fdb17ff1fbda7bacf0fafd445f60d843976`, library
SHA-256 `9461fe3bb77dfabf981af767b586025e76bda574534f2829b59bf5c462955536`.
The coordinator checks the exact original worker fingerprint, compiler
informational version, compiler source/build receipt identity and solver-file
digest. It independently binds the prior summary, bootstrap log and resource CSV
by their fixed hashes, then checks all 560 outcomes and the public resource
receipt. The prerequisite explicitly retains `fullGate=true, passed=false`.
All 58 current vendored files, the exact complete inventory, the upstream revision
and the integration patch must match the a6 manifest before that library can be
reused. The coordinator additionally checks the declared source pins in the
bootstrap builder, WorkerPackage and experimental packager. The
runner requires the loaded Core informational version to equal the supplied
fresh CLI version, checks actual loaded compiler bytes against the captured directory and
the package validates actual library/host/protocol bytes. The coordinator
independently invokes the exact original CLI `--version` and solver `-version`.
All captured compiler DLL identities and hashes, the Core reference metadata and
actual loaded Boogie identities/reference metadata are serialized. These
identities and immutable hashes are evidence bound to a separate build
receipt, not a signed source attestation.

The existing direct-host irrational control uses 1,000,000 resource units and
10 seconds, with a hand-built native Real request that lacks the actual Dafny
generated context. Its passing negative outcome cannot be reused as acceptance
for the original 200,000-resource actual-Dafny fixture. The triage keeps those
differences explicit. The following investigation first compares the emitted
conversion and false goal with this English argument, then compares unchanged
baseline and exploratory receipts. Pricing comes after semantic correspondence.

## Intended remote invocation

The coordinator is a diagnostic wrapper. It records all three original CLI
controls and all runner stages, preserves strict booleans in `summary.json`, and
exits zero only to deliver a diagnostic receipt; that exit is not a green gate.
It must run through the approved CI or queued executor, never on an owner's
interactive machine. The parent supplies paths and exact identity arguments to
`.github/review/b3-real-triage.py`; `--exploratory-rlimits 1000000,10000000` is an
explicit example of two additional limits, not a recommended product default.

```sh
python3 .github/review/b3-real-triage.py \
  --prior-artifacts "$B3_REAL_PUBLIC_PREREQUISITE" \
  --solver "$B3_REAL_PINNED_SOLVER" \
  --dotnet "$B3_REAL_DOTNET" \
  --compiler-source "$B3_REAL_BUILD_SOURCE" \
  --compiler-version "$B3_REAL_BUILD_VERSION" \
  --output "$B3_REAL_NEW_OUTPUT" \
  --exploratory-rlimits 1000000,10000000
```

This is an invocation template for the reviewed remote wrapper; all supplied
paths must be absolute and the output must be new. The outer wrapper records its
immutable public prerequisite download and fresh source/build receipt.

The coordinator runs the fresh current strict 46-control corpus independently,
checks its exact 34 case names and 12 fixed control names without duplicates,
and checks every original expected exit and diagnostic against its result. It
compiles the Java target with the lowercase output basename `b3` and records the
actual jar size, SHA-256, source fingerprint and bootstrap compiler version. The Java
compiler is the exact archive-checked a171 bootstrap; this is a compilation stage,
not a second 560-obligation library verification. Neither the original corpus
expectations nor its acceptance checks are waived. The ordinary worker client
retains its existing ownership/timeout cleanup. The coordinator additionally
requires Linux pidfd support and an initially empty dedicated child-subreaper
scope, using the same adopted-child ownership helper as
[check-b3-package.py](../../../Scripts/check-b3-package.py). Each stage starts a
new Unix session. After the main child exits, natural exit and reaping have at
most ten seconds within the existing stage deadline; that grace sends no signals
and does not extend a stage or worker budget. Output limits remain enforced. The
actual build environment appends `-Dorg.gradle.daemon=false` to `GRADLE_OPTS`.
Gradle documents this property, but a disposable single-use JVM can still fork
and stop after the build. See the official
[Gradle daemon documentation](https://docs.gradle.org/current/userguide/gradle_daemon.html).

On a timeout or failure, bounded TERM/KILL cleanup signals only validated adopted
child instances through pidfds, including orphaned worker sessions. Every later
stage requires an empty child scope; any child remaining after natural-exit grace
or any forced cleanup invalidates that stage, and a failed drain prevents later
stages. This is a dedicated coordinator process-tree scope, without a cgroup
containment or escaped-runtime claim. Before grace and before cleanup, the receipt
captures at most 32 direct-child PID, comm, command-line and start-time observations.
Each comm is limited to 256 bytes, each command line and stat read to 4,096 bytes;
races and truncation are explicit. These observations do not authorize signals.
Residual counts, natural reaping, cleanup signals, final membership and log hashes
remain separate. The supplied absolute .NET path also prefixes PATH for the
existing corpus/worker launchers.

The first focused diagnostic
[run 37227920873](https://github.com/erniecohen/dafny/actions/runs/37227920873)
validated its prerequisite and source seal. Its compiler process exited zero,
but one adopted child remained; cleanup sent 34 signals and left zero residual
children. The compiler log reported a Gradle daemon. The stage therefore failed
before emitting any triage requests or running Real proofs. This operational
failure provides no Real correspondence result. The lifecycle repair is a fresh
source-only checkpoint; it has not been executed. Actual emitted artifacts and
budget comparisons remain pending.

The replacement [run 37229767767](https://github.com/erniecohen/dafny/actions/runs/37229767767) built the fresh compiler with zero cleanup signals or residual children. The diagnostic runner then failed to compile because its `Snippets.ShowSnippets` reference lacked the actual `DafnyCore` namespace qualifier. No Real requests or proofs were executed. The narrow repair qualifies that same public option; original source fixtures, intended option value, queries and resource budgets remain unchanged. A fresh diagnostic source seal records the correction; earlier seals and incomplete receipts remain historical evidence.
