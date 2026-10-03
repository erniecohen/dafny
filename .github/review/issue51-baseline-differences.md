Issue #51: separately tracked baseline comparison failures and unexplained performance differences (2026-10-03).

These observations belong to their exact measured sources and configurations. They do not change the three cardinality admission obligations, justify weakening strict-client rejections, or request a broader library proof campaign. Current integration readiness is recorded separately; no executed final-tip whole upstream suite is claimed.

| Complete comparison | Baseline | Patched | Solvers |
|---|---|---|---|
| [Development, run 37099229727](https://github.com/erniecohen/dafny/actions/runs/37099229727) | `b6ed9f6dcf62cacbe17c5208cf53380393fe27cb` | `0b973a0943653c2379b53394ae407953c42d40a1` | Own default 4.16.0; separate 5.1.0 |
| [Release, run 37101284918](https://github.com/erniecohen/dafny/actions/runs/37101284918) | `edff862a3da391fe2f0142343a5846edf078b2ed` | `d435feef947c4ddc54ab00e094ad926ca7a8f336` | Own default 4.12.1; separate 5.1.0 |

Canonical program verdicts do not change in either solver configuration: 1,092/1,092 development rows and 1,081/1,081 release rows. All 1,920 existing release integration outcomes, including the four failure signatures below, match. Development adds 206 passing Core and four passing language-server controls; release adds the same. Existing deliberate negative proofs remain rejected. Fuel preserves 31 verified / 39 errors; ReadsOnMethods preserves development 68 / 35 and release 67 / 31, including the before/after error positions in both solver configurations.

The library comparison uses `verify`, a zero time limit and JSON logging. The normal verified archive build uses `build -t:lib`, the normal project time limit/budgets and CSV logging. The archive-versus-comparison discrepancy remains unestablished; source resolution alone is not archive verification.

| Library comparison scope | Baseline evidence and remaining outcome |
|---|---|
| Development, own 4.16.0 | All 2,439 rows have identical verdicts. Both retain `LemmaRoundDown` OutOfResource at DivMod.dfy:729; recorded resource count 24,177,102. Normal archive builds pass all seven libraries. |
| Development, 5.1.0 | All 2,439 rows have identical verdicts. Both retain three Errors and five OutOfResource declarations. |
| Release, own 4.12.1 | Exactly two of 2,197 rows improve: `LemmaIndistinguishableQuotients` correctness and its aggregate Std run. The shared `MultiplyDivideLt` Errors row remains at 615,131 resource units. |
| Release, 5.1.0 | Exactly the same two rows improve. The remaining two Errors and 24 OutOfResource declaration verdicts, and their individual resource counts, are identical before/after. |

The quotient proof was repaired separately in [PR #77](https://github.com/erniecohen/dafny/pull/77). The actual unchanged-specification body improves from OutOfResource at 1,035,490 to Correct at 12,506 resource units under 4.12.1, and from OutOfResource at 1,035,510 to Correct at 13,405 under 5.1.0. [Normal archive comparison run 37097759154](https://github.com/erniecohen/dafny/actions/runs/37097759154) establishes that the original release baseline and resolver-only repair both fail that same quotient obligation; [normal repaired release rebuild run 37100112758](https://github.com/erniecohen/dafny/actions/runs/37100112758) verifies all seven. The earlier release baseline client package is explicitly a committed-archive fallback, not a fresh verified baseline package.

[Official library comparison run 37104016524](https://github.com/erniecohen/dafny/actions/runs/37104016524) records the two changed rows per solver before the separate expected-verdict update at `f348294b357e286c7948d831d3a8f566813b5755`. The change accounts only for quotient correctness, the aggregate summary, and subsequent DivMod source-position shifts. Every other existing negative verdict is preserved.

| Shared release full-run failure | Baseline and patched evidence / disposition |
|---|---|
| `github-issue-33.dfy` setup | Both roles request real Z3 5.1.0, absent from the default-only setup. [Focused corrected setup, run 37103584777](https://github.com/erniecohen/dafny/actions/runs/37103584777), installs it without replacing the default solver; both #33 fixtures pass 2/2 on each role. This discharges the setup failure in that focused scope. |
| `server/counterexample_commandline.dfy` snapshot | Both roles retain the same postcondition failure and identical diagnostic signature. The generated character is `\U{0002}` rather than the snapshot's `\0`. The same difference is retained in the focused comparison. |
| `git-issue-817d.dfy` snapshot | Both roles reject the same four invalid array ranges. The actual output has four diagnostics rather than eight including redundant l-value messages. Its negative purpose is preserved; the snapshot was not normalized. |
| `wishlist/git-issue-6158.dfy` proof | Both roles retain the same two loop-invariant entry failures at lines 40 and 45. The specifications and bodies were not repaired in issue #51. |

The complete development run's existing documentation fixture rejection was corrected by explicitly stating its intended reference parent, `trait T3 extends object`; both original generated HTML goldens are byte-identical. The release run's single added positive nonvacuity failure is also reproduced on its unpatched baseline under 4.12.1. Reordering two existing positive assertions proves the legal chain while retaining all four intended false-assertion failures and the unchanged golden. [The focused default/5.1 comparison](https://github.com/erniecohen/dafny/actions/runs/37103584777) checks all eight role/solver combinations; [the reviewed final-tip focused run](https://github.com/erniecohen/dafny/actions/runs/37104427004) passes development 2/2 and release 3/3 changed fixtures, plus exact packages and affected clients. These retests do not constitute a complete final-tip suite run.

Paired already-Correct declaration resource totals differ without changed outcomes. The improved quotient row is excluded from the release paired totals. All values below come from the public GitHub CI comparisons linked above; causes remain unestablished.

| Solver comparison | Baseline resource units | Patched resource units | Changed resource-count rows |
|---|---:|---:|---:|
| Development 4.16.0 | 1,168,320,087 | 1,168,179,147 | 146 |
| Development 5.1.0 | 995,188,700 | 995,190,016 | 94 |
| Release 4.12.1 | 1,002,009,414 | 1,001,977,134 | 20 |
| Release 5.1.0 | 922,567,637 | 922,568,614 | 10 |

The original development normal-archive Exp well-formedness batch 16 rises from 2,051,349 to 11,592,574 resource units. [Focused repetitions, run 37102001834](https://github.com/erniecohen/dafny/actions/runs/37102001834), retain the normal project, existing Exp declaration override, arithmetic solver 2, two cores, assertion isolation and seed 0. All four runs exit 0 with four verified obligations, zero errors and 18 Passed proof CSV rows each. Baseline batch-16 repetitions are 2,051,349 / 2,051,395; patched repetitions are 2,051,349 / 2,051,349. The original spike does not recur and remains unexplained. Small repeat variation is not an explanation of the larger spike.

Those four actual filtered Numbers-module Boogie captures are byte-identical, 3,036,904 bytes each, SHA256 `90017d02d5986bd19d99a62b14a0a2aac7cf5190af8cbbef01b195f3605a952b`. This equality applies only to the captured module under the actual Exp filter; it is not whole-library or whole-suite Boogie equivalence, and does not establish identical downstream SMT requests.

The recorded baseline failures and performance variations stay separate from the parent merge decision. No unmeasured cause is asserted, no admission obligation or specification is weakened, and no extra library proof repair or resource-budget increase is proposed by this record.

The integrated compiler rebuild, [run 37120750092](https://github.com/erniecohen/dafny/actions/runs/37120750092), uses proof source `8133a7952cf3d706ba7483803e4293ca0407e708` after release destination `39da8948dd03e6fe4af465ffac96db70f3188471` is included. All 8,030 archive rows pass. Relative to the prior approved normal build, archive total resource units move from 1,017,224,007 to 1,017,292,133, with 1,388 resource-count rows differing. The cause of this 68,126-unit increase is unestablished. All seven archive program and manifest payloads remain byte-identical to the prior verified set; fresh container hashes are recorded in the merge-readiness record. This is serialized archive-payload equality, not a claim about all Boogie or SMT inputs. The current unpatched destination still fails the quotient proof, with targets skipped; no fresh baseline package is claimed. The current normal CI matches all unchanged expected canonical/library verdict rows.
