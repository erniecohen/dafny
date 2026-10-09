# Caller preparation and sole legacy call check

Retain the full new caller preparation/traversal/fuel state but use the single
checked procedure requirement rather than local caller assertions. No duplicate
proof or support omission. Generate current local checking expressions for a
strict lexical typed comparison with formal-to-actual substituted procedure
requirements. Reject mismatches and unsupported scopes. Other translation
families remain legacy in the hybrid. Native baseline/current controls require
exact complete input/outcome/resource fidelity; the false control must be Invalid.
This is causal diagnosis, never product acceptance or a case-specific policy.

The existing caller-summary compiler workflow is the build entry point at this
scratch revision; its enable script selects these placement instrumentation
sources. Runtime selection uses the separate caller-placement diagnostic flag.

The audit runs before identifier resolution. Its cloned substitution handles
unresolved formal names, requires frozen identifier actuals with matching
types, rejects all formal/actual binder capture and unknown expression nodes,
and leaves the actual verifier program untouched. Full native input controls
still establish instrumentation fidelity.

Coercions fingerprint their destination type and canonicalized operand, rather
than embedding the printed operand with its original binder names. Boogie
TypeCoercion equality depends on that destination type; different target types
remain different, and no bound name escapes lexical canonicalization there.

The following scratch revision additionally isolates private-argument
allocatedness inside the selected caller's certified preparation. It omits only
two matching current-heap assumptions, retains every other command object and
full traversal/fuel state, and checks the same six procedure requirements once.
That support reduction is diagnosis only, never a product policy or acceptance.
