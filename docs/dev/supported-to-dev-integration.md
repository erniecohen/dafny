# Full supported-line integration into dev

Source boundaries: supported line `1256f92b0b494e8f1bf52aaec145d36a8a862f33`,
dev `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f`, completed #169 port
`9029c0461d13aa73d7c40dddd260a26db65deaef`.

The [coverage map](supported-to-dev-coverage.json) records all 127 nonmerge
supported commits and 52 supported PRs from public Git and GitHub metadata.
The merge retains full supported history and reconciles source overlaps.
The integration remains under validation.

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

Pending fresh compilation, independent producer capture, full native controls,
normal exact-head CI and suite/library OFF/ON comparisons. No partial port run
certifies the combined tree. Enabled resource failures, source maintenance
costs and intentional verdict changes must be individually recorded.
