# Recursive module invariants (issue 466, stage 4)

The model is the flattened module of `design/xmod-recursion.md`: the raw declarations
of every member belong to one mutually recursive definition. The existing group
phases create invariant types and close their inherited-invariant and raw-member
information before checking bodies. The ordinary member-invariant construction,
conversion, well-formedness and termination obligations then apply in each member.
No axiom is added by this stage.

The extension condition is a restriction on that definition, rather than a fact
an importer may use. It runs once for all declarations, after the group's bodies
have been resolved. It temporarily reveals each member's own declarations while
following type synonyms and the bodies and specifications in an invariant's cone.
In particular, a provided synonym cannot hide a path back to the invariant's owner.
The temporary scope ends before verification; export views still restrict proofs.

Dependence on the owner is reachability through component types. A recursive visit
initially returns false to break a cycle. That provisional result is not a completed
negative summary: another branch may subsequently reach the owner. Positive answers
can be retained; negative answers are searched again in a later query. This computes
the least reachability solution without discarding a path because it passed through
an active recursive visit. The changed analysis is restricted to recursive groups.

The hidden `--legacy-recursive-module-invariants` restores the previous per-module
analysis and retained negative results. It is for comparison and migration only;
it accepts inconsistent invariant definitions. The ordinary option-off path is
unchanged, and `--recursive-modules` remains hidden, experimental and opt-in.

`git-issue-466-stage4.dfy` checks two- and three-member datatype invariants,
witnesses, reference types, and reachable false assertions. Its attacks are:

* `stage4-hidden`: a liar invariant calls a sibling's predicate whose unbounded
  quantifier ranges over a provided synonym of the owner. Removing the combined
  analysis scope accepts the definition and verifies `WouldBeBad` with `ensures false`.
* `stage4-dependence`: a bounded quantifier first visits a recursive datatype cycle,
  followed by an unbounded quantifier over a declaration visited on that cycle.
  Retaining the provisional negative accepts this definition and verifies false.

Each attack is rejected normally and verifies under the hidden legacy option. The
site mutations independently remove the combined scope and the negative-result
invalidation, so neither attack relies only on disabling the whole extension check.
