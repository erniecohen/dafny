# Structured terminal exit comparison

This scratch-only comparison changes exit layout before Boogie names structured
blocks and assigns their successors. The selected source ends with a conditional
whose two branches have simple anonymous fallthrough tails. The already translated
certified pure exit fragment moves into those tails before native CFG resolution.
One branch retains its original command/dependency objects; the second has exact
formula/non-ID metadata copies and validated distinct dependency entries. Every
original source command remains once, fuel/preparation/publication remain intact,
and each reachable exit path has one mandatory postcondition check. The native
assertion counter includes the additional static branch check.

This differs from the rejected post-resolution placement experiment, which left
structured block naming/successor construction unchanged. Support is generated
only from the proposition/local state. Body traversal audits object retention;
it does not collect terms or generate support instances from the body. The
original conditional guard object remains unchanged. Unsupported shapes reject
the diagnostic rather than silently changing semantics. A native full-input control
and a genuine false-entry control are required in both axiom settings at the
unchanged resource ceiling. This is not an adopted product policy.
