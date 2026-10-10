# Development port of consistent obligation checks

This draft adapts the final reviewed implementation from #168 (1256f92b) to
`dev` based on 858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f. Validation is pending.

The feature remains experimental and off by default. Single-check local
lowering, certified independently licensed contract preparation, source
guards and scopes, and separate caller/callee termination translators follow
the reviewed design. The registered issue 100 gate includes the sequential
Schorr-Waite postcondition example with support definitions, missing-edge and
feasible-state controls, and labeled/nested two-state positives and negatives.

## Development differences

- Dev is Dafny 4.11.1 against Boogie 3.5.5. Supported-line 4.11.0 receipts
  establish the reference design, not this port's acceptance.
- Dev has no additional-axioms option or local integer-literal identity helper.
  Its existing can-call translation is retained; the feature is tested under
  both resolvers without the unavailable additional-axiom axis.
- Dev lacks the supported line's separate callee termination translator. The
  opt-in path adds that distinction, preserving the caller's termination context
  through labeled previous-heap replay, function WF, lambdas, and statement
  expressions. The default-off path preserves dev's prior termination lowering.
- The old-heap rebasing helper is supplied locally because dev lacks it.
- Constant-field type-argument substitution retains the existing dev behavior.

The producer inventory is captured independently from this branch. The original benchmark sources and ceilings are unchanged. The registered
dev-only oracle records two independently reproduced baseline exceptions below;
the general cases table remains unchanged. Proof repairs under
#170 and the later default-on decision under #171 are separate work.

AI assisted implementation and validation. A new review is required before merge.

## Branch-specific producer audit

The independent Roslyn capture found 280 groups across 26 verifier files. All
118 structural tests passed in the [development diagnostic](https://github.com/erniecohen/dafny/actions/runs/38049408314); the subsequent committed 280-group inventory gate also passes.

The differences from the 287-group supported registry are explicit:

- Dev has no local additional-axiom literal helper or the supported line's
  lambda-handle allocation helpers. Its original permission interfaces remain.
- Dev has floating-point NaN, special-function, arithmetic and conversion
  checks absent from the supported source: two NaN checks, four extra special
  function checks, twelve extra ordered-WF assertions and nine extra conversion
  assertions. These retain their existing specialized lowering. Generic result
  constraints continue through the guarded type-membership adapter.
- Dev's co-recursive suspended-value checks remain mandatory before destructor
  observation, constructor arguments, co-recursive results and let-pattern
  binding. The original check's `$Is` predicate remains a separate proof rule;
  the opt-in clause adapter does not replace it with a free assumption.
- Dev's constant-field type substitution, general-trait casts and existing
  lambda handle construction remain unchanged. New contract replay retains the
  original lambda frame policy and caller termination context.
- Opaque blocks, forall exports, iterator yield/exit contracts, match/if
  completeness, witnesses, frames, termination and allocation retain the same
  specialized obligations as the audited reference, adapted to the dev APIs.

The default-off caller/callee termination branch reproduces dev's prior
translator selection. Enabled replay uses separate source contexts, not an
assumption that identifies the two heaps. The positive and negative replay
fixtures inspect individual declaration outcomes, so an independently invalid
assertion cannot conceal acceptance of a false caller precondition.

## Native development comparison (diagnostic)

At compiler source `6ffb00e14f7409c1ac9f4d2363959689096d2cc3`, the
[paired native run](https://github.com/erniecohen/dafny/actions/runs/38049710147)
uses Dafny 4.11.1, .NET 8, Boogie 3.5.5 and checksum-pinned Z3 5.1.0.
The baseline compiler is built independently from the dev base named above.
No source hints, original tests, ceilings or expected-verdict tables change.

All 1153 uniform suite programs completed in every mode. Default-off has zero
verdict differences and exactly identical resource observations across all
8100 recorded VC batches (1,406,010,712 RU). Of the 1153 inputs, 1115 emit
nonempty parseable proof JSON; 38 have no proof JSON, including one parser-error
input with an empty file. Their actual diagnostic verdicts remain in the rows.

Enabled mode has 58 row differences: 48 change only diagnostic source locations,
and ten change summary counts. Of the latter, four split combined constraints
into separate invalid conjuncts (ForLoops, ResultInTypeNewtype,
ResultInTypeSubsetType and git-issue-356-errors2); their source remains rejected.
`Absy.Foo` in git-issue-2703 has an invalid division-by-zero contract WF check in
both modes. Its separate correctness proof becomes valid under the independently
required WF domain facts; the complete source remains rejected. The only Invalid-to-Valid VC remains inside that independently rejected source.

The remaining five summary changes are resource transitions: ExtensibleArray
and Lucas-up lose completion; TwoDuplicates and Primes gain completion;
SnapshotableTrees has one fewer exhausted batch. Per-batch results also expose
exhaustion moving between two methods in ExtensibleArrayAuto, two SchorrWaite
batches and two SmallestMissingNumber methods. Six formerly valid batches exhaust
and five exhausted batches become valid. The original files are retained for
review and proof-maintenance assessment; aggregate RU alone does not justify
these changes. Enabled total is 1,401,305,594 RU across 8100 batches.

The seven standard-library runs contain 2432 declaration groups. Baseline and
default-off both report 2424 Correct, three Errors and five OutOfResource, with
zero verdict differences. Their RU totals are 1,348,166,370 and 1,348,165,801.
Seventy-one declaration totals differ by at most 146 RU; batch counts match.
These runs retain the Makefile declaration order, whose small resource-count
variation across runs is documented by the existing standard-library runner.
Enabled mode reports 2425 Correct, two Errors and five OutOfResource, with
1,359,132,616 RU. LemmaRemainder, LemmaModNegNeg and RadixStrictlyIncreasing
lose completion; EncodeBVLengthCongruentToZeroMod4, ConcatenatedProducer.Invoke
and LimitedProducer.Invoke gain completion. Lemma2To64's arithmetic correctness
proof changes from Errors to Correct; its power-of-two equalities are valid.

The two target-specific runs for cs and notarget still complete all 132 proofs,
but return exit 2 because the enabled preparation yields unnecessary-requires
warnings at Duration-notarget-cs.dfy:196. These warning failures are retained,
not relabeled green. The existing helper preserves the Makefile options and
all seven actual run exit codes. The [recovered aggregation](https://github.com/erniecohen/dafny/actions/runs/38051335855)
reads these complete immutable receipts and explicitly records unavailable proof
JSON. The original issue 100 proof remains unproved; its registered dev result is explicitly marked KNOWN LIMIT.

## Correctness gate findings

The [independent development probe](https://github.com/erniecohen/dafny/actions/runs/38050181337)
completes all 333 core tests, 118 obligation structural tests, the committed
280-group inventory gate and the editor invalidation test. The original
strict supported-line issue 100 oracle fails on native dev; its actual receipt is retained. Its diagnostic replay executes all
194 verifier invocations without weakening that registered oracle.

Every enabled-mode positive and false control in that replay passes its original
expectation except the original issue 100. This includes the full Schorr-Waite
sequential-postcondition file and all its supporting definitions, missing-edge
and feasible-state negatives, independently false caller preconditions, and both
labeled/nested two-state positives. Every declaration of the replay controls is
checked individually in its verification JSON, preventing an unrelated false
assertion from masking a soundness error.

The two two-state positive controls fail only when the feature is disabled:
their valid labeled recursive calls cannot prove termination under the dev
legacy caller/callee translator. Enabled mode correctly separates the caller
measure from the label's callee previous heap. The default-off implementation
retains dev's old lowering deliberately; the [independent native baseline replay](https://github.com/erniecohen/dafny/actions/runs/38051013330)
confirms both failures under the original dev compiler and both resolvers.
The original fixtures remain unchanged.

The original issue 100 still exhausts its 16,000,000 resource ceiling in both
resolvers, with the feature both off and on. Its explicit-assertion and final-true
variants verify at roughly 450,000 RU in every corresponding mode. The emitted
exit proposition is the same; the final-true variant adds a tautological source
assertion to the surrounding implementation. The [native baseline receipt](https://github.com/erniecohen/dafny/actions/runs/38050879264)
confirms the original exhaustion predates this port. The [controlled native Boogie probe](https://github.com/erniecohen/dafny/actions/runs/38051833019)
reproduces the limit with native Dafny's argument encoding, name normalization,
pruning and solver options for seeds 0, 1 and 7. Inserting only a tautological
Boogie assertion into the original makes all four procedures pass; removing it
from the final-true Boogie input restores the resource limit, despite retaining
that input's scopes. This isolates an inherited backend VC/proof-context
sensitivity, rather than a false proposition or a defect requiring new axioms.
The direct monomorphic Boogie comparison without the native options passes all
four procedures and is diagnostic evidence only. No extra assertion, source
hint, new axiom or higher ceiling is added to the original.

## Explicit dev acceptance profile

The governing reviewer permits investigated performance limits while requiring
soundness and default-off compatibility. The dev registered gate therefore has
three narrowly documented, artifact-backed outcomes different from the supported
line's oracle:

- Original issue 100: the unchanged native dev proof records `KNOWN LIMIT` at
  16,000,000 RU. Its `wfi` declaration must be OutOfResource and its three
  supporting declarations must be Correct, with nonempty individual VC results.
  Timeouts, parser errors, internal failures and other outcomes fail the gate.
  This does **not** claim the original theorem has a successful dev proof receipt.
- The two valid two-state replay controls require Correct with the feature on;
  feature-off requires only the original caller's termination declaration to be
  Errors, all other declarations Correct, matching the independent dev baseline.
- Every soundness negative remains a strict Invalid check, both independent
  source failures and every declaration inspected. The Schorr-Waite positive and
  supporting definitions remain strictly Correct.

Normal exact-head dev CI is pending this explicitly reported profile. Enabled
suite/library resource transitions and warning failures remain reviewable data,
not proof repairs hidden in benchmarks. No new review or merge is claimed yet.
