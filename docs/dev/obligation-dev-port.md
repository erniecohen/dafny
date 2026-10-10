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
- Constant-field type-argument substitution retains the existing dev behavior.

The producer inventory must be captured independently from this branch. No
expected-verdict table or original benchmark is changed. Proof repairs under
#170 and the later default-on decision under #171 are separate work.

AI assisted implementation and validation. A new review is required before merge.
