# Follow-up review governing final validation

Source: [PR #168 follow-up review](https://github.com/erniecohen/dafny/pull/168#issuecomment-6091926707) (2026-10-10).

This request supersedes the earlier pause on broad validation. It authorizes
full-suite and standard-library comparisons after correcting the fingerprint
oracle, and forbids further speculative tuning of `RemoveFactor`. PR #169
remains unchanged until separately authorized. The seven report items below
are completion requirements, not claims of completed validation.

## Review text

A followup review:
Thanks for the substantial progress. I support the current architecture and think the candidate is ready for broad validation, but **not yet for merge approval**. Please proceed as follows.

### 1. Fix the remaining fingerprint-oracle defect

In `ObligationFingerprint.cs`, bound identifiers and free identifiers share the same string namespace. For example, these formulas can receive identical fingerprints even though they are not alpha-equivalent:

```text
forall x: int :: x == x
forall x: int :: bound0 == x
```

Here, `bound0` in the second formula is a free constant. Use disjoint structural representations for bound variables, free variables, and renamed preparation temporaries. Add a direct Boogie-AST regression test, and rerun the structural comparisons.

This is a test-oracle defect, not a demonstrated verifier soundness defect.

### 2. Freeze the candidate and run the whole suite

Once the fingerprint fix is complete, please run the full verifier test suite and standard library comparison against the pinned baseline.

Compare:
- Baseline.
- Candidate with the feature disabled (default).
- Candidate with the feature enabled.

Cover both additional-axiom settings and the required resolver configurations. Use identical solver versions, options, resource ceilings, and test inputs.

Please report verification verdict changes, resource exhaustion, timeouts, and performance changes. In particular, identify substantial per-declaration regressions, rather than relying exclusively on aggregate performance.

Also run the newly reported `requires P ensures P` example from issue #100, including its direct, immediate-assertion, and wrapped-predicate variants. Report whether the candidate achieves the intended assertion-equivalence behavior.

### 3. Do not continue open-ended `RemoveFactor` tuning

The observed `RemoveFactor` results are:

```text
Baseline: VRRRRRRR
Enabled:  RRRRRRRR
```

This remains an unresolved regression under the existing classification rules. However, the evidence is insufficient to justify further speculative changes to the translation.

Please retain the regression in the report, but do not expand the seed search, increase resource ceilings, insert source assertions, or introduce special-case lowering.

Revisit it only if the whole-suite comparison or a concrete translation-level analysis identifies a generalizable defect.

### 4. Account for baseline nondeterminism

The `MembersSpec` investigation provides credible evidence that resource-count differences can occur even when emitted Boogie is identical, including between repeated executions of the unchanged baseline.

Please distinguish genuine regressions from such variation. For suspicious default-off performance changes, include baseline-versus-baseline measurements.

Default-off semantic and verification-verdict compatibility remain hard requirements. Do not silently weaken the existing performance acceptance criteria; instead, explicitly report cases where baseline variability makes exact resource equality unsuitable.

### 5. Complete the soundness review

Please provide a concise soundness argument for the final implementation, particularly the replay of contract well-formedness preparation in assumption mode.

For each checked contract clause \(P_i\), establish that every preparation assumption is justified by the incoming context and previously established clauses \(P_1,\ldots,P_{i-1}\), without depending on \(P_i\) itself or later clauses.

Also confirm that the implementation preserves the original checking/publication semantics, guard and heap contexts, and visibility/scoping requirements, and does not accidentally use body reads-frame assumptions or other unverified facts.

The existing negative tests are valuable, but they do not replace this argument.

### 6. Provide a final acceptance report

Please produce one consolidated report containing:

1. Full-suite and standard-library comparison results.
2. Default-off compatibility evidence.
3. Enabled-mode verification and performance regressions.
4. Remaining unresolved cases, including `RemoveFactor`.
5. Results of the new issue #100 reproducer.
6. The soundness argument.
7. Your recommendation on merge readiness.

**My preference is to avoid further product modifications unless testing reveals a concrete, generalizable defect.** In particular, do not pursue additional fuel or triggering changes solely to improve individual benchmarks.

The current design looks substantially better aligned with issue #100 and the earlier extensive review. The priority now should be to establish whether it is sound, compatible, and sufficiently performant across the full suite.

Please complete this validation before requesting merge approval.
