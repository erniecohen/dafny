# Content-addressed cache acceptance

The persistent resolution, translation and task-result layers are opt-in. Their
composition must preserve current-source evidence as well as verdicts and
per-VC resource counts. A hit count alone does not establish this contract.

## Implemented layers

| Boundary | Public implementation and acceptance |
|---|---|
| Resolution | Typed snapshots, prerequisite identities, source rebinding and the independent `--resolution-cache-check` oracle; [warm materialization](https://github.com/erniecohen/dafny/pull/581) and [quantifier identity comparison](https://github.com/erniecohen/dafny/pull/612). |
| Translation | History-free module inputs, content-addressed translated graphs, early cutoff and [finalized global translation inputs](https://github.com/erniecohen/dafny/pull/602). Global admission still runs on the current program. |
| Results and CLI reports | Exact task identities, resource-bounded outcomes, current-source assertion/dependency reports and native model payloads; [CSV](https://github.com/erniecohen/dafny/pull/585), [resource exhaustion](https://github.com/erniecohen/dafny/pull/589), [JSON](https://github.com/erniecohen/dafny/pull/591) and [complete reports](https://github.com/erniecohen/dafny/pull/593). |
| IDE | [Restart, overlay, selection and cancellation lifecycle](https://github.com/erniecohen/dafny/pull/597), [whole-module assertion reports](https://github.com/erniecohen/dafny/pull/607), and [failed/mixed reports, models and code actions](https://github.com/erniecohen/dafny/pull/609). |

## Composition gate

The quantified and resource-exhausted cases in
`PersistentCacheLifecycleTest.WholeModuleQuantifiedAndExhaustedReportsRemainNativeEquivalent`
combine resolution checking with whole-module IDE replay, with one and two
translation workers. Fresh, cold, warm and audited runs compare verdicts,
per-VC resources, assertion trees, hovers, diagnostics and code-action edits.
Warm and audited runs require actual checked resolution hits; the warm run also
requires whole-module hits and no solver starts. The audit uses native tasks.

`git-issue-261-cache-stack.dfy` adds independent server-process coverage of the
same boundaries for a quantified counterexample, including model requests.
Corrupt resolution records must miss while later report records remain usable.
A changed quantified body must match an independent current-source run and
must not replay the previous task's result. These tests complement the existing
individual-layer corruption, eligibility, storage, edit and cancellation tests.

Corpus acceptance additionally compares independent fresh, cold, warm and
audited verification of the standard library and the compiler fixture with
all layers enabled together. It compares every non-timing field in CSV, JSON,
text and TRX, current diagnostics and per-VC resources. Profiles must establish
resolution hits without body resolution on the unaudited warm route, whole-module
hits without translation/preparation/solver work, and both independent oracles
on the audited route. A failure or resource-exhausted baseline is preserved,
not reclassified as a successful proof.

## Closure boundary and later work

The issue's conservative three-layer cache contract can be accepted after the
reviewed changes merge and their integration gates pass. This does not promise
a cache hit for every program or reporting mode. Unsupported plugins, incomplete
payloads, proof-refactoring analysis, time-dependent execution and unsupported
resolver configurations retain the native path. Their eligibility guards are
part of the contract.

The design's P11 minimal observable export views remain a later invalidation
optimization. Current prerequisite structural fingerprints conservatively
invalidate on changes that a smaller visibility projection might exclude.
Implementing P11 requires an audited resolver read boundary, nominal identities
and origin rebinding; dropping hidden bodies from a hash alone is insufficient.
Further lazy/compact snapshot materialization and replay binding-index tuning
are also performance refinements, not missing correctness adapters.

The original study's [validation record](261/VALIDATION.md) describes only that
study's models and proposal patches. It is not the current implementation's
acceptance ledger. The implementation references and composition gate above
identify the later production work.
