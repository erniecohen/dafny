# Exact-argument native function law focused diagnostic

This six-job diagnostic requests 291 compiler invocations: all 93 original #82
verification routes on three arms, plus four parser solver/AX profiles on three
arms. The three compilers are reused from completed public build run 37347177735,
artifact 11361575690 (`default-off-compilers-shipped`). No compiler build or
source composition is performed by this workflow.

The arms are the repaired feature-free product `bbf90260694e2e3004030c35b57f012a98b7c65d`,
the feature product `4e50e86cde6ab2ef6cab0762ffc4df59d646dc15`, and that same
feature product with the issue #165 conditional exact-argument proposal
`e050312cf42cef71a9f0e404c4b98df1f75d96b26306da67276846fa0bb919e9`.
The candidate full tree is `5b924f4012464b2f8aac845c1a193236c22ccf78`,
Core tree `95fb3e9316e02259e4a004734d621a5c03951212`, and Source tree
`f8715951b1d481236cab3f6bab3e1aed04ca3a49`. Original build/source receipts,
actual version strings, all 329 archive files and modes, 208 managed/apphost
components and seven packaged libraries are bound separately for each arm.
A fresh public artifact API receipt must match the exact ID, name, run, source
head, digest, size and unexpired status before reuse. Payload hashes remain
independent requirements. Original build exits are copied provenance; fresh identity/version exits are
recorded as new observations. Whole extracted compiler closures are checked
before and after each paired job.

All five #82 wrapper sources, their source closure, 93 proof RUNs and 57 literal
output checks are frozen from the previously reviewed focus packet. The parser
profiles load the complete original Std project and select `Std.Parsers` tasks.
The 5.1.0 and 4.12.1 solver archives, original refresh/AX flags, caps, cores,
bodies, contracts and output oracles remain unchanged. Parser AX-off omits
`--additional-axioms`; AX-on supplies the original wrapper flag. #82 routes use
their own literal flags and default/pinned solver macros. The proposal's
AdditionalAxioms gate does not imply that AX-off callbacks are restored.

CSV/JSON/BPL observation options are retained unchanged. A translated BPL hash
does not identify a solver query or session. Omitted JSON seed fields and a CSV
typed default do not establish an actual solver seed.

The workflow is restricted to scratch branches of erniecohen/dafny. Setup and
step failures are retained while diagnostic jobs continue without claiming
acceptance. Actions completion and a complete diagnostic report do not establish proof
acceptance, source adoption, default-off compatibility, or cost parity. Raw
exits, declaration outcomes, warnings, resource/batch maps, original output
checks, BPL receipts and any missing/setup/closure failures are retained. The
full canonical suite and full Std comparison remain separate requirements.
