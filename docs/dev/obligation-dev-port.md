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

The producer inventory must be captured independently from this branch. No
expected-verdict table or original benchmark is changed. Proof repairs under
#170 and the later default-on decision under #171 are separate work.

AI assisted implementation and validation. A new review is required before merge.

## Branch-specific producer audit

The independent Roslyn capture found 280 groups across 26 verifier files. All
118 structural tests passed in the [development diagnostic](https://github.com/erniecohen/dafny/actions/runs/38049408314); the subsequent normal inventory gate must check the committed capture.

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
