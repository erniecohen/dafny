# Constraint-variance verdict registration

These three new verify-first drivers join both verdict tables and the regression
manifest. No existing expected verdict is changed.

- `git-issues/git-issue-491.dfy` is a resolution error: twelve subset-type
  declarations have a conflicting occurrence in their constraint. Its later lit
  commands check both resolvers, the legacy false proofs, an unavailable generic
  body and a type test. Each negative example contains a false proof accepted by
  the legacy option.
- `git-issues/git-issue-491-newtypes.dfy` is a resolution error at its covariant
  newtype declaration. The legacy option verifies its false proof. The refreshed
  resolver is explicit in the first command because generic newtypes require it.
- `git-issues/git-issue-491-positive.dfy` has five verified declarations and three
  deliberate assertion failures, one reachable `assert false` in each module.
  It exercises monotone existential quantification, declared covariance,
  recursive generic summaries, contravariance and type tests, including a
  generic newtype. Its declarations also resolve successfully. The legacy
  option leaves the same three assertions unproved.

The new default-on rule adds no axiom, so the expected rows are the same with
additional axioms enabled. The tables are sorted by program name; sorting the
previously appended rows does not change their verdicts. The coordinator still
runs the repository's CI after pushing, including its ordinary suite and
standard-library default/legacy comparison.
