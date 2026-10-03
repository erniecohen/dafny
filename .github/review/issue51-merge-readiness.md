Issue #51 parent repair: merge-readiness record (2026-10-03).

Approval and integration of [PR #77](https://github.com/erniecohen/dafny/pull/77) do not merge either parent repair into its destination.

The resolver repair preserves representation contracts, direct retention of implementing parameters, and rejection of expansive nominal cycles. It retains cache, visibility, refinement, cancellation, and translation-entry coverage. It adds no axioms, library exemption, weakened reference-field checks, or indexed-type analysis. The `!` migration is an exported cardinality-contract change; the developer document explains parent, callback-output, stored-family, ghost-invariant, filtering/batching, and set-reader dependencies. Ordinary variance, equality/non-heap characteristics, other specifications, and executable bodies are preserved. The bounded release quotient proof uses existing lemmas to establish its unchanged specification at the original budget. Intentionally rejected strict clients remain rejected.

| Integration item | Exact revision |
|---|---|
| Approved PR #77 head | `f348294b357e286c7948d831d3a8f566813b5755` |
| PR #77 integrated into release repair branch | `d6aaa806120ad6a5176c976501bb88443a94fd9d` |
| Identical tree at both those revisions | `d1ab1ff5fb5cd9e5a3b4aa8b26aa816f45e8cb35` |
| Development destination before parent merge: `dev` | `b6ed9f6dcf62cacbe17c5208cf53380393fe27cb` |
| Development prepared input | `7a31673bebe3f7cc2d373c80b66de170902517a5` |
| Release destination before parent merge: `review/4.11.0` | `39da8948dd03e6fe4af465ffac96db70f3188471` |
| Release prepared input, including current destination and CI regression | `8133a7952cf3d706ba7483803e4293ca0407e708` |
| Release committed archive/package input | `c9c58fd433ebcd96725b41fd86f17428854a29f9` |
| Development destination after parent merge | Not merged; closure condition remains unmet |
| Release destination after parent merge | Not merged; closure condition remains unmet |

The development input adds only the packaged-library CI fixture, helper, and workflow step to the previously packaged `eb99b48e77a8e83184fa9146e3fa0954b7a36a5f` repair. Its compiler, library proof/configuration inputs, and seven archive bytes are unchanged. The release input includes the destination's [PR #76](https://github.com/erniecohen/dafny/pull/76) compiler changes; its normal archives were freshly rebuilt and packaged at that integrated source. No Std source/proof/configuration input changed during integration. The historical release package is not substituted for the current compiler's package.

| Normal archive configuration | Development | Release |
|---|---|---|
| Dafny source line | 4.11.1 | 4.11.0 |
| Own `DefaultZ3Version` | 4.16.0 | 4.12.1 |
| Arithmetic solver / cores | 2 / 2 | 2 / 2 |
| Main project resource limit | 5,000,000 | 1,000,000 |
| Target project resource limit | 1,000,000 | 1,000,000 |
| Main safety time limit | 300 seconds | 300 seconds |

These are normal verified `build -t:lib` settings. Existing declaration attributes remain unchanged, including Exp's existing resource override. No unexplained budget increase, bypass, assumption, or hand-edited archive is included. Z3 5.1.0 remains a separate comparison configuration.

The seven archive SHA256 identities below apply only to their stated branch/configuration. Development hashes match the existing fresh verified set; the integrated release hashes identify the exact fresh outputs committed at `26f7e863ad65dc86c2aec82ba15e55b8352f8efa` and checked against the actual embedded bytes.

| Archive | Development SHA256 | Integrated release SHA256 |
|---|---|---|
| `DafnyStandardLibraries.doo` | `df23dbfcb551552d007dd41f6eb2b24c4f6b6eb0de613dbd1c40d4cb3882337c` | `16a01675f6298df98b5bc437fd94cc24c0eff1ca9a0cf225d4ee432114612848` |
| `DafnyStandardLibraries-notarget.doo` | `a57f92d3013d44d138c2b817ebd12129da6c189ddf70328a798a114f83787fc8` | `1f563ce75899801e30cf0d9d4328131f97b98adceff0a74dda5bdfc8687775a7` |
| `DafnyStandardLibraries-cs.doo` | `06a9b6766a855ebfae0dc96daafe51adee19a1aa95d9b5081009aad15935de07` | `055f378501b17fb9c05a4abc7b1601e5deb255cc4e9a442a2eb1cf2ccfa32f46` |
| `DafnyStandardLibraries-java.doo` | `3c01c19d6c1b6d044b7a5c221ead09ebddecff08e7d1796c863ab3bf7f01ec76` | `8a1c192569495c035a7b73086c5d26681317a50f188d81b8e3ff63ddca1f6adb` |
| `DafnyStandardLibraries-js.doo` | `35ccd21546dff84255cd52c3b4ff2709a0377d097987828c93f673c21b1e58cb` | `725cb2415dd641c30b395e81454e7aa79a3b991c34bbcbb528e5ea4417adcddc` |
| `DafnyStandardLibraries-go.doo` | `0f843240a7cf46040f558cda90ac78863140fcd1966e553c11560e824c311e1a` | `4b3bd450e5d745b59a878edbabaccf013c203c7cd50ea16cc63819dc99dd5749` |
| `DafnyStandardLibraries-py.doo` | `a030b5b41b4c0d5027bce5601490050ad733c6823e4daa9b29f0179aee567cda` | `2907d7fdac75e2f99de398452d4c88c54dd389b179692bff3fbe331468ad79fa` |

| Applicable validation | Source/configuration and scope | Result |
|---|---|---|
| [Normal development archive comparison](https://github.com/erniecohen/dafny/actions/runs/37097759154) | Development baseline and repair; own Z3 4.16.0; normal projects and budgets | 7/7 freshly verified archives per role; 8,275 Passed proof rows per role |
| [Approved release normal archive build](https://github.com/erniecohen/dafny/actions/runs/37100112758) | Proof source `65ec91fc945f93640c8d3d528851357716392f1f`; own Z3 4.12.1; normal projects and budgets | 7/7 freshly verified archives; 8,030 Passed proof rows |
| [Reviewed final packages, clients and changed fixtures](https://github.com/erniecohen/dafny/actions/runs/37104427004) | Development `eb99b48e77a8e83184fa9146e3fa0954b7a36a5f`; release `f348294b357e286c7948d831d3a8f566813b5755` | Both candidates embed their exact seven freshly verified archives; 6/6 package checks each; 84/84 client expectations across four roles; 20/20 verified runtime clients; focused development 2/2 and release 3/3 |
| [Development lightweight packaged-library regression](https://github.com/erniecohen/dafny/actions/runs/37120931944) | `7a31673bebe3f7cc2d373c80b66de170902517a5`; ordinary limits; branch-default solver | 2/2 packaged checks pass; one Passed correctness row and 46,528 RU each; version `4.11.1+7a31673b`; independent package/CLR audit 25/25 |
| [Release integrated normal CI](https://github.com/erniecohen/dafny/actions/runs/37120804371) | `8133a7952cf3d706ba7483803e4293ca0407e708`; normal review workflow | All normal CI jobs pass. Independent official comparisons find 0/1,081 canonical changes and 0/2,197 library changes in each solver lane. 206 Core, 4 language-server, 2 emission, 74 registered Lit, 4 option-guard and 1 editor-invalidation tests pass. Packaged check 2/2 passes at 41,883 RU each |
| [Release integrated archive/package gate](https://github.com/erniecohen/dafny/actions/runs/37120750092) | Baseline `39da8948dd03e6fe4af465ffac96db70f3188471` and patched `8133a7952cf3d706ba7483803e4293ca0407e708`; own Z3 4.12.1 and normal budgets | Candidate: 7/7 verified archives; 8,030 Passed rows; 1,017,292,133 RU; actual package exit 0, exact seven resources and 6/6 smokes. Baseline: the same quotient failure, 7,917 Passed/1 Failed, exit 4; six targets skipped. Independent audits: baseline 12/12, candidate archives 37/37, staged package 58/58 |
| [Committed release package repeat](https://github.com/erniecohen/dafny/actions/runs/37121718442) | `c9c58fd433ebcd96725b41fd86f17428854a29f9`; archive product `26f7e863ad65dc86c2aec82ba15e55b8352f8efa`; strict unchanged compiler/proof/config and exact committed-archive guards | Actual exit 0; 6/6 verified package checks pass at 41,509 RU each; five runtimes output `7` / `file ok` and bytes 01 02 03; exact seven committed/fresh/PE resources match; independent audit 61/61; version `4.11.0+fcb2042d.review.d8f4946d` |

The lightweight normal-CI regression verifies a `Std.Wrappers`/`Std.FileIO` client through the published executable with `--standard-libraries`, then verifies and runs its target-specific C# implementation. It requires nonempty all-Passed proof CSVs, successful exits, empty stderr, exact output `7` / `file ok`, and exact file bytes 01 02 03 at ordinary verification limits. This is a persistent regression; the broader reviewed package gate separately covered all five runtime targets.

The complete historical comparisons are [development run 37099229727](https://github.com/erniecohen/dafny/actions/runs/37099229727), baseline `b6ed9f6dcf62cacbe17c5208cf53380393fe27cb` versus `0b973a0943653c2379b53394ae407953c42d40a1`, and [release run 37101284918](https://github.com/erniecohen/dafny/actions/runs/37101284918), baseline `edff862a3da391fe2f0142343a5846edf078b2ed` versus `d435feef947c4ddc54ab00e094ad926ca7a8f336`. They establish their recorded full-suite comparisons, not an executed final-tip whole upstream suite. The final reviewed tips had changed-fixture and package/client retests. The new normal-CI canonical/regression matrix is also distinct from the complete upstream Core, language-server and integration suites. Existing comparison failures and unexplained performance variations are tracked separately in [the baseline record](issue51-baseline-differences.md), with their baseline evidence. Later publication commits containing only these CI records change no compiler, proof, configuration or archive inputs. GitHub records their exact PR heads; they are not presented as an executed final-tip whole upstream suite.

If integration changes compiler sources, library proof/configuration inputs, or archive contents, the affected gates must run again and this record must identify the actual tested revisions. Parent merges remain a separate step. Issue #51 is to close only after both `dev` and `review/4.11.0` contain the repair and their respective matching verified library sets, with the exact destination revisions recorded above.

The committed release package tar SHA256 is `5d2ce4585b29cb4610cffa0527f96f7a003636512b43f8d9447476317408bc6b`. The development Build package tar SHA256 is `3e0336d34ebc5992303232f0a65f20715f6e559a69df6bedee9a57a13f1ac52e`. These identities belong to the executed source revisions named above; later CI-record publication commits are metadata only.
