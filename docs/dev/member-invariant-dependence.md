# Member-invariant dependence through cycles (issue 504)

The extension condition restricts an invariant of `T` from referring to the set
of values of `T` or a type whose values depend on `T`. A datatype component is
such a dependency even if the path passes through a datatype cycle. For example,
`B = Loop(C) | End(T)` and `C = C(B)` make both `B` and `C` dependent on `T`.
Searching `B` visits `C` before the `End(T)` branch. The false answer obtained by
encountering the active visit to `B` is only a cycle-breaking answer; retaining
it for `C` lets a subsequent unbounded quantifier over `C` escape the restriction.

The analysis now retains only positive answers after a visit returns. Every
retained positive is justified by an actual finite path to the owner (or one of
the existing direct-dependence cases). An active visit still supplies false to
break cycles, but a false result is removed on return. A later query therefore
searches again with a fresh recursion stack. For that query, an ordinary depth-first
reachability search finds every path to the owner. A cycle with no such path still
returns false. No declaration, verification condition, axiom or assumed fact is
added: the change only rejects definitions that fail the existing extension
condition. The analysis runs only for member invariants.

The hidden `--legacy-member-invariant-dependence` restores the former cache,
including its unsound negative summaries. It is for migration and comparison only.

The regression `git-issue-504-dependence.dfy` enters a two-type cycle through
either type and a three-type cycle through each of its three types. Every case
has a `WouldBeBad` lemma with `ensures false`: normally the unbounded quantifier
is rejected, and under the legacy cache all five contradictions verify. In a
disposable build, remove the negative-result invalidation in
`InvariantExtensionCondition.DeclDependsOnOwner`. The same five false proofs must
verify; this is the site mutation, independently of the legacy switch.

`git-issue-504-positive.dfy` constructs values of invariant types whose cones
quantify over genuinely independent two- and three-type cycles. Its false controls
construct those values and then attempt reachable `assert false` statements;
those statements must fail. The feature requires the refreshed resolver. The
legacy resolver's option-dependency rejection is checked separately.
