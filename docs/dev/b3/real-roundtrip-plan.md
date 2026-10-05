# Bounded native Real round-trip preparation (issue 124)

This is a source design for the opt-in experimental B3 backend. The source checkpoint
has no compiler, test, proof, or runtime acceptance receipt. Opaque ground-context
projection, worker fixtures, and new corpus routing are separate future reviews.

## Order and original evidence

Run only after the existing structured CFG, definition availability, unsigned guard
certificates, and exact G3 replay partition have been validated. Keep those original
programs and checks available. `B3DefinitionContexts.ValidatePartition` requires an
exact replay; a prepared program is a separately checked submission, never a
replacement witness for original source availability.

The backend constructs internal provisional request headers with empty hashes,
captures bounded owned snapshots, then computes bounded hashes and applies all
current protocol/type/hash validation before transformation. Neither the Request
adapter nor ProtocolValidation hashes live input before capture. Require
original request/context association and unchanged live source hashes. An association
failure rejects preparation. Require the same context/request objects, mask IDs,
definition origins and source-obligation associations; re-capture before and after
planning to reject observed mutation. This controlled producer boundary is not an
atomic snapshot guarantee against arbitrary concurrent ABA mutation. Optional work/output-limit fallback is only to the
fully typed, hash-bound owned original snapshot. It never uses changed live input.

After independent relation validation, hash the final submitted bytes and use them
in the existing work key. Record original/final hashes and producer-version evidence
distinctly. Preserve mask identities and the exact source-obligation arrays. The
worker, protocol schema, native library, solver arguments/pins/options/resource
limits, shared original-unit deadline, and completion classification remain unchanged.
There is no second request after Unknown and no conversion of Unknown into Failed.

## Universal rule and state invariant

The [SMT-LIB Reals_Ints theory](https://smt-lib.org/theories-Reals_Ints.shtml)
interprets ToReal as integer injection and ToInt as floor. For every integer i,
floor(injection(i)) = i, including negative integers. Therefore the sole arithmetic
rule is exact typed unary `ToInt(ToReal(e)) -> e`, with e/result Int and inner result
Real. This does not imply `ToReal(ToInt(r)) = r` for arbitrary Real. All Real values,
including algebraic irrationals, remain available.

Only already justified native Operations are matched. Existing source coercion/Body
and active-definition recognizers remain unchanged. There is no function-name match,
new axiom, prelude fact, UF expansion, constant folding, or Check deletion.

On one control path, a dictionary E records typed expressions satisfying
`state(x) = Eval(E[x], state)` at the next ordinary statement. Remember only numeric
Variable, IntegerLiteral, RationalLiteral and unary ToReal/ToInt chains. Rows are
already expanded at creation and carry exact free-variable sets.

For x := rhs, rewrite rhs using old E, invalidate x and every row containing x,
then remember the rewritten rhs only if it contains no free x and is in the narrow
grammar. Keep the assignment and its original order. Havoc invalidates named keys
and all their dependent rows. Checks, assumptions, axioms, learned facts and solver
results never create rows. Self-dependent assignments stay without a new row.

Traverse ordinary Operation and Label children without adding algebraic laws.
Application, Quantifier/pattern, Let and BitvectorOperation subtrees are whole
substitution barriers. They still receive exhaustive bounds/type validation. An
outer conversion rule may return an unchanged Int subtree; it does not enter a
binder to substitute coincidentally matching names.

Blocks thread E. Conditional guards use entry E; Conditional/Choice alternatives
receive independent copies and clear E at the join. Keep every branch and guard.
Labeled bodies start empty and clear E afterwards; Exit/Return clear E. Retain
unreachable tails with empty E. Clear E before every Loop field and after its body;
retain the protocol's empty native-invariant restriction.

Pinned `Verifier.ProcessLoop` checks entry invariants, havocs body assignment targets,
then vets header invariants. Incoming remembered values cannot survive that havoc.
Original explicit invariant checks outside the Loop remain in their original
positions. All assignment targets and transfers stay, preserving native havoc and
continuation scheduling.

Under E, each rewritten Assign/Assume/Check/guard has the same value as its original.
Checks keep their IDs, Learn flags, source identities, labels, order and paths;
their learned facts are equivalent. Every function/type/variable declaration and
selected Axiom stays identical. Each final mask receives a fresh dictionary. No
original availability/unsigned certificate is minted from a rewritten occurrence.

The independent relation checker walks source and target in lockstep with its own
state-transfer rules. It rejects altered control/targets/child order/check metadata,
stale variable substitutions, changed declarations/axioms, and arbitrary expression
replacement. A hash alone is not the semantic relation check. This is a bounded
executable check plus English correspondence argument, not a formal compiler theorem.

## Bounds and all-mask preparation

Keep the existing per-request node/depth/32MiB and all-mask aggregate bounds. Charge
every copied occurrence and exact numeric/string bytes. Capture input before hashing
and before unbounded allocation. Strengthened input admission permits at most
400000 copied collection slots per request and 800000 across all masks, charged
before builder allocation. Charge total text characters and a conservative JSON
escaped-string bound (six bytes per UTF-16 code unit plus quotes) against 32MiB
before encoding. Reserve the filled canonical hash before measuring provisional
headers. Include source identities, headers, configuration and both base
and derived type fields; reject inconsistent type aliases rather than canonicalize
them. Input admission failure rejects the whole preparation; it is not optional
rewrite exhaustion. Preparation has conservative optional limits of
1024 rows, 100000 free-variable memberships and 800000 work events per original unit.

First produce bounded recipes and account complete output growth/bytes for every
mask; materialize only after admission. Recipes count repeated expression occurrences
even if their materialized immutable nodes are shared. Unchanged barriers receive
full child/binder/pattern counts. Charge a conservative 512-byte structural reserve
per recipe node and escaped string bounds. The larger of unchanged full request
bytes and its captured escaped-text reserve covers all headers/declarations/axioms
(including the filled hash). Reserve relation work before creating target arrays.
The producer charges source visits, expanded remembered-chain visits, dictionary
invalidation and branch copies. The checker reserve is twice that work plus four
visits per target occurrence, covering its independent embedding tests and exact
expression comparisons. Exceeding that checker reserve rejects preparation rather
than return a partially checked candidate.

The independent relation entry separately captures its source and target before
hashing/type validation. Each capture uses the same 100000-node, 400000-slot,
128-depth and 32MiB text/escaped-text/serialized bounds. These per-entry captures
are transient and do not create an availability certificate. Preparation admits
all at-most-16 target masks together under 200000 nodes and 64MiB, with shared
recipe/relation work and free-membership budgets. Original input capture also
shares the 800000-slot aggregate bound. Failure in an extra relation capture,
hash/type check or semantic relation rejects the entire preparation as Unsupported;
it never returns partial output or silently falls back. Only optional recipe,
growth or work exhaustion discards the entire candidate and returns the already
validated owned originals. No partial output,
missing Check, unbounded lookup/clone or stale source association is accepted.

## Frozen structural control census

The source-only Phase A delta is 18 methods and 41 cases. Existing tests remain.
Worker execution and two new source corpus cases require separate review; structural
controls do not count as worker anti-vacuity or operational Real acceptance.

| Method | Fixed cases |
| --- | --- |
| DirectTypedFloorEmbeddingRewrites | variable, negative Int, zero Int (3) |
| RealInverseWithoutEmbeddingIsUnchanged | arbitrary Real, negative nonintegral rational (2) |
| ConversionAssignmentsReachBothOriginalChecks | Fact (1) |
| AssignmentInvalidatesKeyAndDependentExpressions | Int overwrite, temporary overwrite, target overwrite (3) |
| SelfDependentAssignmentDoesNotCreateRememberedRow | Fact (1) |
| HavocInvalidatesAllDependentRows | Fact (1) |
| BranchDictionariesStaySeparateAndJoinIsCleared | Conditional, Choice (2) |
| LoopHeaderBodyAndExitStartWithoutIncomingRows | Fact including nonempty-invariant rejection (1) |
| LabeledAndAbruptControlClearContinuationRows | Labeled, Exit, Return (3) |
| CheckAndAssumeNeverCreateOptimizerRows | Assume, learning Check, nonlearning Check (3) |
| BindersApplicationsAndWordsRemainSubstitutionBarriers | Quantifier, Let, Application, BitvectorOperation (4) |
| RewriteRetainsAllChecksLabelsOriginsAndLearnFlags | Fact (1) |
| G3MasksKeepOriginalPartitionAndStableFunctions | Fact (1) |
| ConcurrentPreparationsUseFreshState | Fact (1) |
| MalformedTypedConversionsAreRejectedBeforeRewrite | arity, inner type, result type, unknown variable (4) |
| OutputGrowthCountsCopiedOccurrencesBeforeAllocation | below/at/above a node limit, below byte cap (3) |
| BoundedTraversalRejectsDepthAndMetadataExcess | below/at/above depth limit (3) |
| ForgedOrMutatedRewriteCertificateIsRejected | original hash, Check ID, Learn, control (4) |

## Source inventory representation

`real-roundtrip-source.json` binds canonical Git blob lengths/hashes and the declared
checkout lengths/hashes separately. The project file has CRLF checkout bytes under
`.gitattributes`; its canonical Git blob has LF bytes. Other payload files in this
checkpoint have identical Git and checkout bytes. This inventory is source evidence,
not a compilation, test, proof or signed build receipt.
