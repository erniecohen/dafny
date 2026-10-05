# B3 product source integration for issue 116

This records a source-only projection under [issue 116](https://github.com/erniecohen/dafny/issues/116). It does not report a build, proof, package, default-compatibility result, or backend acceptance for the integrated source. B3 selection remains opt-in. The existing support matrix and discrepancy ledger retain their scope.

## Fixed inputs and projection

The aggregate parent is `001312cec1a37611c3c66befa9f4c1ee34c3a3ce`, with last product commit `8333daa60e2f2ee456068369f94c141898cde875`. The reviewed source parent is `f71aa19c0f82ce98033b95b2bd25896915dc0ee2`, with last product commit `7e0f321d22bb6ac5c119788e8bb0fbdcf2f8469a`. Both descend from `a0103f02c231907d34250283810c40a1f7d68519`.

[Product inputs](product-integration-products.json) bind all 101 changed fork-defined product files, their canonical Git modes, blob identities, lengths and SHA256s: 1,181,015 bytes. [Companion inputs](product-integration-companions.json) bind 67 further paths: 431,892 bytes. Their row seals use UTF-8 JSON with sorted keys and compact separators, without a terminal newline. Each input file itself retains its original bytes.

The product projection uses the complete final versions from the source parent. It includes the prerequisites for native Real, native words, scoped literal-definition contexts, typed CFG correspondence, direct unsigned wrappers, producer-owned If guards, Real round-trip preparation and finite opaque-ground projection. Applying only the final preparation commits would omit dependencies. The 65 ordinary companion files also transfer exactly from that source parent, including the Reference Manual, source fixtures, proposed issue-124 regression expectations, release notes and semantic documents.

Two paths have explicit reconciliation. The regression list preserves the aggregate order and contents and appends `git-issues/git-issue-124.dfy` exactly once. Package documentation keeps the historical initial-subset receipts and states that they do not qualify a package for the changed schema-3 source. Its final prose is reviewed separately. Historical source manifests retain their original identities and remain historical evidence.

The aggregate review workflow, diagnostic routes, README, support matrix, discrepancies, work items, and existing verifier/standard-library verdict and resource baselines are preserved. No diagnostic coordinator, input route or workflow hunk is projected from the source branch. The actual new product commit is recorded in the two-word version ledger only after that commit exists.

## Historical product chronology

These are the 35 original product commits in source order. They record provenance, not a claim that each is a minimal dependency. The projection receives a new commit identity; historical receipt identities are not rewritten to describe it.

- `039e81b500a949805293f11a8911fb159ce4099f` — feat(b3): add checked native Real language and solver layers (#124)
- `a2e55481340cdac6a9cd3f53ceecd64b66696c8e` — feat(b3): extend checked worker schema for native Real (#124)
- `3183ea02413c7ba5c287d1fcea16afee645786e6` — feat(b3): normalize exact Real literals and eligible coercions (#124)
- `cdf03df72ac63fcd8f1b10b8a3b1788c9ef3682c` — test(b3): add exact Real controls and refresh checked source identity (#124)
- `5d550b7300fe82bc2132d20114ae6f06d1323d83` — Retain unsupported power control after native Real division support (#124)
- `07a7ddbe6b3b032d58a1bc219d7cac45cca5bac9` — fix(b3): resolve native Real checker and signed parser types (#124)
- `642321eadf54344fefbce485a2250c213ee46373` — fix(b3): connect numeric checker facts and total signed parsing (#124)
- `052f688814a4abd2d6cd0f3ad0a150ceda0d6e58` — fix(b3): expose checked numeric operand types by index (#124)
- `521d55a1f9cd1091bcab5c7da2b19d6854a745a8` — feat(b3): add bounded native bitvector language and SMT representation (#124)
- `3c831d42dbe660df93122627b5eb337a6fea90b2` — feat(b3): validate typed bitvector IR in protocol schema 3 (#124)
- `ec78e27cc97a2b009dcf15b0d83cf55c29cfa2d6` — fix(b3): export solver expression alias with public lowering API (#124)
- `f75f779770cd04ae9468e63efa7005a436383de2` — fix(b3): resolve BV accounting patterns and native sort printing (#124)
- `ce06a7fda37c533c84d82b5d34b30cda446e06a5` — feat(b3): normalize owned native bitvector primitives (#124)
- `f8eb0456ec8255a0fedaaa8d5d131fb55faa0ee9` — feat(b3): record bounded source visibility metadata (#125)
- `bf582d3fa7a0e8b3c0db9919f3fe302e2b6d3697` — feat(b3): replay source-owned literal definitions in scoped contexts (#125)
- `16c72f96443a3a17bf6e8614e5767ec1bbc80139` — fix(b3): guard definition availability across original path subsets (#125)
- `8b6874290581c82cb9d52b5cfb6f96263ac19633` — fix(b3): validate typed structured and raw CFG correspondence (#125)
- `be17f37a262938f9394ec82e1a04e08922b735c5` — fix(b3): retain typed Boolean guard and literal definition correspondence (#125)
- `d6382cd060f674e923b7bc04cbdcefeca1a71980` — Repair typed definition recognition and entry visibility for #125
- `0f75ee1b1dedc224a33f1049e31630a2fc29c093` — test(b3): retain imported opaque definitions and reachable false controls (#125)
- `e2f9a02cf75c4b4de079228d3865b0333368749b` — docs(b3): reconcile combined native and visibility approximation scope (#124)
- `7340beab0e23e3755d7da7ee4af8906fec0c3319` — feat(b3): lower owned rotations through portable word primitives (#124)
- `83d291a01e6c84aff0fa33c4c56a757978f86254` — Traverse native word arguments in B3 definition contexts (issue 125)
- `547845dc7e2eeb5f214aa8f99ffdef6e2bc008f5` — Restore the exact resolved return-label checks in B3 runtime controls (issue 124)
- `2dc8444ddafa24d5e6d397f0855efbb212d78c22` — Normalize certified direct unsigned wrappers (#124)
- `b482c00911c399613c0d348885037b68a0f703cb` — Declare the B3 host primitive fixtures separately so their controls compile (issue 124)
- `70150d254e68921874a32424dd01368e0ba81ade` — feat: retain producer-owned If guard certificates (issue 124)
- `e3d57a2b78002c4261a08d49e88f8ad83859ddaf` — feat: certify unsigned conversion in producer-owned If guards (issue 124)
- `c664ac058c57e91fadd0f7da140fad954fe87841` — feat: preserve compound unsigned guard checks and false controls (issue 124)
- `572ebc88e6e3078d4dbe65f7a1f2eab3767307d2` — fix: keep unsigned guard field capture read-only (issue 124)
- `a7ac2fa2b0f8cda996bcd6634ccab3a4a5c1c6e7` — Implement bounded typed Real round-trip preparation for B3 (#124)
- `b82e8c27efa6fb4acdadfc4e3dadd786b5196bec` — Bind B3 Real work identity to owned submitted configurations (#124)
- `9a2f5d28d9d5ac81948142f747f945c06b6fddad` — issue124: project bounded opaque ground contexts with an independent relation
- `6f1c23c4006a94c93cae8ec5a5479760c2a842b8` — issue124: charge exact projected arrays before materialization
- `7e0f321d22bb6ac5c119788e8bb0fbdcf2f8469a` — Fix the opaque projection evidence local name collision (#124)

## Evidence and remaining gates

Public [run 37273577355](https://github.com/erniecohen/dafny/actions/runs/37273577355) passed a separately reviewed current Core/test compilation and 123 structural controls: 47 opaque-ground preparation cases, 41 Real round-trip cases and 35 unsigned-guard interactions. Its exact source head is `affc2a870fedd0c1668dc221e47735952bd2c7d1`; its product is the source-parent product above. This focused receipt does not qualify a worker, corpus, package, regression, full suite or default-backend parity claim. The earlier [run 37270458214](https://github.com/erniecohen/dafny/actions/runs/37270458214) stopped at Core compilation with the local-name conflict repaired by the final product commit; it remains a failed historical receipt.

A later gate needs a fresh finite inventory of every selected method, class, display identity and data row, plus immutable first accepted compiled outputs and strict result/definition/entry associations. Proposed scalar denominators remain unexecuted until that complete inventory and the actual outcomes are inspected. Do not use stale diagnostic counts or infer full acceptance from the structural focus.

The schema-3 vendor manifest binds 60 files and has SHA256 `04acac15b45763c86af4b47b50b4415a9568d22ac304b86a0ba492d11bdb1722`. Reuse of an unchanged library requires separately inspected exact source, patch, bootstrap, proof CSV, DLL and 37-runtime evidence. The older 557-proof bool/int library is a different input and cannot substitute. Otherwise a fresh verifying bootstrap must use the approved compiler, solver and proof scheduler.

CLI and worker corpus acceptance still require the runtime-asset qualification. The SDK observer is unexecuted; its null/false prerequisites do not license missing dependencies or a runtime-selection claim. The issue-124 native regression expectations remain proposed until executed. Actual B3 positive and reachable-false controls require separate complete final-worker verdicts; Unknown cannot count as Failed. Native collection/default resource parity, its nine dependency edges and four source contracts, full backend acceptance and release acceptance remain pending.
