# Current shipping default-off paired measurements

This diagnostic reconstructs the repaired existing-language compiler from the
original public source and a reviewed Core-only patch, and compares it with the
issue113 compiler on the same unchanged original inputs. It records actual
failures, warnings, timeouts, missing artifacts, resource counts and translated
Boogie bytes. Diagnostic Actions success is orchestration, not proof success.

The input source is `4b2742fb9924eed47c7bd40779e4283a1e65ce6d`; the repaired
product is `bbf90260694e2e3004030c35b57f012a98b7c65d`; the feature product is
`4e50e86cde6ab2ef6cab0762ffc4df59d646dc15`. Both sides contain the independently
reviewed prerequisite composition and the finite-support repair from PR160.
The baseline includes no extended-newtype option or operation views. Baseline
composition checks complete/Core/Driver tree IDs and every patch path/hash.
Its build uses the recorded baseline product as SourceRevisionId; the baseline
commit need not be fetched. The final public product is checked out exactly.

The canonical denominator is unchanged: 1,081 old suite programs on each
original AX-off/on wrapper, and seven old Std parts on both AX wrappers under
Z3 5.1.0 and the canonical reference 4.12.1. The input manifest binds 4,770 files,
including old committed libraries, and regenerates the original plans. Canonical
suite precedence retains 16M RU, declaration-order normalization 0, one core, no seed,
original explicit exceptions, and the original helper's Unicode-option drop.
Std retains its old projects, budgets, sorted input lists, four cores, and default
declaration order. Neither compiler receives the extended option. AX-on injects
the old wrapper flag before own arguments; own explicit settings still win.

The default dispatch has 36 canonical paired jobs plus two separately labelled
whole-source shards for the five newly merged #82 literal regression wrappers.
These add 93 verification pairs (186 invocations), 54 OutputCheck mappings and
three literal output comparisons. Their source flags, AX selections, refresh
settings and proof caps are unchanged. `%review-z3` retains 5.1.0; `%z3` retains
the shipping default 4.12.1. They do not replace or extend the old canonical
oracle. Exact source and check files are frozen at the merged PR160 source.
Optional global refresh adds eight labelled suite jobs; optional original
Unicode-setting controls add one job per chosen refresh profile. The completed
unchanged development evidence is reused explicitly, not rerun by this workflow.

Matched arms run sequentially on one host in balanced deterministic order, with
at most four concurrent suite pairs, one Std pair, and two #82 source pairs.
The workflow limits concurrent jobs to 12. Original wall caps remain 900 seconds
for suite/literal invocations and 5,400 seconds for Std, with 240 minute job caps.
Incomplete arms remain incomplete even when the workflow records failures and
exits 0. No cached or mixed observation directory is permitted.

Actual Core/Driver/main and all published DLL/deps/runtimeconfig/apphost hashes
and packaged library hashes are checked before and after each measurement.
Solver executable hashes and version receipts are retained and checked after
measurement. JSON logging and BPL printing observe the same verification call;
they add no seed or schema. An existing literal own bprint setting keeps its
original precedence and argument, including an unexpanded `%t`; its exact fresh
output file is retained separately. Original source inputs are checked again
when the cohort finishes.

Both raw and normalized BPL hashes are retained. Normalization changes only
identified version comments and known roots in comments/captureState diagnostic
attributes; semantic text, IDs, checksums, order, triggers and axioms remain.
Emitted-program equality is not engine VC generation or solver-query equality.
Per-declaration and per-batch resources are compared separately. An omitted
JSON randomSeed field remains unknown, with field-presence metadata; no actual
seed is inferred. Every resource movement remains visible, with no tolerance,
waiver, or exact default-off resource-parity claim.

The #82 adapter implements only the frozen CHECK/CHECK-NOT regex subset of the
upstream enumerator and retains every literal check and raw observation. Its
strict text comparisons and expected exits are supplemental diagnostics. The
canonical upstream regression harness and full new-source gates remain separate
requirements. Matched expected failures and allowed warnings are not verified
proofs, and reference Std failures are never called green.

Pure preparation commands are `plan.py --check`, `prepare.py inputs`,
`prepare.py compose`, `matrix.py`, and `repair82.py plan`. The compose command
writes only its requested reconstruction checkout; it performs no build/proof.
Only the workflow's explicit build steps and the run subcommands execute engines.
This source package is prospective until its exact public artifacts are measured.

For the complete shipping comparison with both separately labelled additions,
dispatch the installed scratch workflow with `global_refresh=true` and
`semantic_unicode=true`. This requests 48 paired jobs and 8,914 compiler
invocations: 4,364 original-source pairs plus 93 new existing-language #82 pairs.
The default dispatch requests 38 jobs and 4,566 invocations. These workload
sizes are requested denominators, not completed measurements.
