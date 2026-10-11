# Full supported-line integration into dev

Source boundaries: supported line `1256f92b0b494e8f1bf52aaec145d36a8a862f33`,
dev `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f`, completed #169 port
`9029c0461d13aa73d7c40dddd260a26db65deaef`.

The [coverage map](supported-to-dev-coverage.json) records all 127 nonmerge
supported commits and 52 supported PRs from public Git and GitHub metadata.
The merge retains full supported history and reconciles source overlaps.
The [tree map](supported-to-dev-tree.json) records every path changed on either
line from the common base, with public blob identities and exact-copy checks.
It is structural evidence; overlapping source still requires semantic and
native review.
The integration remains under validation. Existing dev counterparts are listed
individually in the map, including the partial #136/#162 relationship; no
counterpart is treated as blanket source equality.

## Source adaptations

- Preserve dev floating-point types, conversions, constants, special fields,
  approximate expressions, refreshed resolver paths and existing type-system
  defaults. Extend cycle-safe numeric ancestry to floating-point carriers.
- Restore the fixed Boogie dependency and all prelude repairs. Retain upstream
  functions that depend on allocation while combining captured-value allocation
  and finite-support boundaries.
- Restore shared additional-axiom helpers, pure-arrow allocation facts and
  default two-state termination around #169's reviewed obligation lowering.
- Retain dev 4.11.1 identities and build with the supported .NET 10 migration.
  Runtime artifacts and compiler output remain branch-specific; dev uses its
  exact source hash, without a supported-line release tag or product hash.
- Combine regression registrations, including the dev output-filename alias
  control. Expected verdicts begin at dev's recorded baseline and must be
  updated only from individual demonstrated outcomes.
- Preserve dev Statistics, DateTime, ZonedDateTime and Base64 source additions;
  carry the supported quotient-proof/source follow-up alongside them.
- Restore strict original issue100, supporting definitions, Schorr–Waite and
  labeled/nested two-state checks. Earlier partial-port acceptance exceptions
  are removed; the combined tree requires its own native evidence.

Issues #170 and #171 remain separate. This integrates accepted supported-line
changes; the flag remains experimental and off by default.

## Validation

The [three-source build receipt](https://github.com/erniecohen/dafny/actions/runs/38137142292)
records actual successful native publish outcomes for original dev, supported
reference and combined source `be862609d9147fb80dcea8df96eceddc722bb1ee`.
The combined command reports `4.11.1+be862609` and uses the fixed Boogie package
with .NET 10. Compiler and Boogie assembly hashes are in each identity artifact.

The [five-mode suite/library comparison](https://github.com/erniecohen/dafny/actions/runs/38137406071)
and [fresh controls/core/producer diagnostic](https://github.com/erniecohen/dafny/actions/runs/38137493963)
are running. There are 192 registered regressions, including the union of both
lines and native constrained float-wrapper positives, invalid NaN introductions
and feasible-state false controls in both resolvers and both lowering modes.

The [first complete native control receipt](https://github.com/erniecohen/dafny/actions/runs/38136538378)
passes the strict original #100 registered oracle at its original ceilings, with
all supporting definitions and false controls. The independent diagnostic
interpreter records 386 verifier invocations and zero failed checks. Structural
checks pass 118/118. Core tests pass 396/397; the sole failure is the stale
producer registry, replaced by the independently captured 288-group inventory.
The [producer change map](supported-to-dev-producer-changes.json) records every
added, removed and token-changed group from the previous 280-group dev port.

Most replacements route existing permission queries through the supported
additional-axiom-aware helper, with the default permission formula preserved.
Remaining additions are the main lambda-handle family and pure-arrow allocation
helpers, plus datatype/default-value and element-initialization traversal.
Existing semantic family classifications are preserved; current verifier file
hashes are checked against the capture before importing it.

The editor project-option test failed with request cancellation during initial
document resolution, before any option-change assertions. Diagnosis and a fresh
passing focused run are required. This is a test failure, not accepted evidence.
The first new floating-wrapper probe rejected an incompatible resolver option
before verification; the corrected paired test remains pending.

Complete native dev interaction controls, branch-specific library
binary reconciliation, normal exact-head CI and the complete comparison remain
pending. No partial-port run certifies the combined tree. Enabled resource
failures, source maintenance costs and intentional verdict changes must be
individually recorded.
