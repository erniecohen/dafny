# Fixed native format contracts

This is source inspection for the private six-case draft. It is not runtime proof,
native query parity or native cost parity evidence. No private diagnostic artifact
is included. The fixed source controls have one file and no includes.

Dafny baseline source is `b07c038737d6713b6d1a5848d7568bdc972de7dd`.
`Source/DafnyDriver/JsonVerificationLogger.cs`, lines 28–34, emits token filename,
line, column and success-description without converting the filename to an absolute
path. Lines 51–57 emit `vcNum`, `outcome`, `runTime`, `resourceCount`, and `assertions`.
Scope serialization emits `name`, `outcome`, `runTime`, `resourceCount`, `vcResults`;
the document root contains `verificationResults`. Native batch outcomes use
`Valid`/`Invalid`; corresponding scope outcomes use `Correct`/`Errors`. Optional
proof-dependency fields and nonzero `randomSeed` remain native output. The draft
accepts only the exact fixed basename or exact absolute/file-URI source path;
it does not use a suffix match or resolve another relative directory.

`Source/DafnyDriver/CSVTestLogger.cs`, lines 72–77, emits exactly this unquoted header:

```
TestResult.DisplayName,TestResult.Outcome,TestResult.Duration,TestResult.ResourceCount,RandomSeed
```

It writes five interpolated fields from the actual test result, sorts rows by
Duration, and uses `unknown` if RandomSeed is absent. Fixed smoke identifiers
contain no comma. The draft matches the native `(Passed/Failed, ResourceCount)`
multiset to JSON and retains the duration/seed text without using wall time as cost.
A changed schema, quoted/comma-containing identifier or incomplete batch log fails
closed and requires review; the parser does not invent an alternative log format.

Boogie source is `73a0e214a87df85fc058270268c1d0706fd05bc9`. Its direct resource chain is:

* `Source/Provers/SMTLib/SMTLibInteractiveTheoremProver.cs`, line 524, assigns
  `resourceCount = ParseRCount(await SendVcRequest("(get-info :rlimit)"))`.
  `GetRCount`, line 679, returns the stored count.
* `Source/Provers/SMTLib/SMTLibProcessTheoremProver.cs`, lines 1531–1541,
  parses that reply using `int.Parse`; an invalid reply returns -1 and is refused.
* `Source/VCGeneration/Checker.cs`, line 266, returns the prover's `GetRCount`.
  Its check initialization resets the prover before the VC (lines 321–339).
* `Source/VCGeneration/Splits/Split.cs`, lines 905–916, obtains the prover count
  and assigns it unchanged to `VerificationRunResult.ResourceCount`.
* Dafny's native JSON logger assigns that batch field directly, as above.

The native `CheckSat` loop in `SMTLibInteractiveTheoremProver.cs`, lines 188–290,
preserves its first verdict as the global result. A sat reply can lead to model/path
retrieval, a blocking assertion and another check. Later unsat terminates this
counterexample search but leaves the global outcome Invalid. Each check overwrites
the stored count with its own raw `:rlimit` reply. Therefore a native JSON batch
count is the **final raw reply for that VC**, which need not be its first count,
a per-check delta or a sum. The draft retains every check and its reply/count;
actual `(reset)` commands delimit VC groups. It matches each group's first-verdict
outcome and final raw count to the JSON batch multiset. Unknown, budget failure,
malformed pairing and any incomplete denominator are rejected. No cost is adjusted.

`Source/Provers/SMTLib/SMTLibProcess.cs`, lines 140–159, sends `PingRequest` after
each request; `SMTLibSolver.cs`, line 10, defines it as `(get-info :name)`. Thus the
ordinary captured command sequence is check-sat, name-ping, rlimit-request,
name-ping. The draft requires that source-grounded sequence and retains all pings
in the complete stream and exact prefix hashes. Its framing only inspects bytes;
it never sends a parsed or normalized query back to a solver. For this exact image
it requires one sat/unsat response and one rlimit response per check; duplicate or
alternative replies require review and cannot produce acceptance.

Actual acceptance remains pending compilation and the reviewed fixed six native
smoke runs. The earlier eleven lifecycle controls establish ownership/relay
qualification only. Six smoke verdicts would still establish no whole-program
native query or resource parity.
