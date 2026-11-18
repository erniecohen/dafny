# Recursive module invariants and refinement (issue 466)

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

Dependence on the owner uses the general member-invariant analysis from issue 504
(`member-invariant-dependence.md`), without any group-specific cache behavior.
Only completed positive answers remain cached; negative answers reached through
an active recursive visit are searched again. This applies to all programs with
`--member-invariants`, including both single modules and recursive groups.

The merged base already accepts concrete groups with invariants. Its per-module
extension-analysis scope can miss a dependency hidden behind a sibling's provided
synonym and accept an inconsistent definition. The combined group scope is a
default-on soundness repair for that existing experimental feature. Its hidden
`--legacy-recursive-module-invariants` opt-out restores only the previous
per-module analysis scope, for comparison and migration. It does not disable the
general cache repair; that repair retains its separate hidden opt-out,
`--legacy-member-invariant-dependence`. The ordinary option-off path is unchanged,
and `--recursive-modules` remains hidden, experimental and opt-in.

`git-issue-466-stage4.dfy` checks two- and three-member datatype invariants,
witnesses, reference types, and reachable false assertions. Its attacks are:

* `stage4-hidden`: a liar invariant calls a sibling's predicate whose unbounded
  quantifier ranges over a provided synonym of the owner. Removing the combined
  analysis scope accepts the definition and verifies `WouldBeBad` with `ensures false`.
* `stage4-dependence`: a bounded quantifier first visits a recursive datatype cycle,
  followed by an unbounded quantifier over a declaration visited on that cycle.
  Retaining the provisional negative accepts this definition and verifies false.

Each attack is rejected normally. The hidden-synonym control verifies with
`--legacy-recursive-module-invariants`; a site mutation independently removes only
the combined scope and must also verify its `ensures false`. The dependence attack
verifies with issue 504's `--legacy-member-invariant-dependence`, which disables
the general cache repair. The same driver also rejects the single-module issue
504 cases with `--recursive-modules` enabled, including with the group scope
opt-out. The group dependence attack must also remain rejected under that scope
opt-out, since the cache repair is independent.

`git-issue-466-clause5.dfy` rejects raw `is`, explicit `as` and implicit conversions
in sibling function bodies, reached directly or through a third module. Finished
operands in the cone and raw operands outside it verify. Returning false from
`ConvertsRawValue` for group owners makes all six attacks resolve; this control
establishes resolution acceptance, rather than a false proof, for these attacks.

## Refinement and abstract groups (stage 5)

Only plain imports may form the group's cycle. Refinement bases, abstract-import
interfaces, and bindings inherited through refinement must remain in strictly
lower components. The existing edge classification rejects cycles through any
of these dependencies before signatures are cloned. Replacement remains deferred.

The existing refinement obligations also apply to a member in a group. Its
inherited callables are vertices of the shared call graph. The counterpart,
decreases-origin and edge checks of `BoogieGenerator.InheritedTermination.cs`
determine whether an inherited recursive component is unchanged; a cycle closed
through a sibling makes the inherited termination check a checked assertion.
Added invariant conjuncts use `BoogieGenerator.InheritedInvariants.cs`: the base's
proof justifies only its own conjuncts at an inherited site. A new conjunct is
checked in inherited witnesses and bodies, and a member newly made raw is
reverified. These obligations already exist; this stage removes admission errors
only for the cases governed by them, and adds no axiom or unchecked assumption.

An all-abstract group is the same fixed point with its abstract types and lower
interfaces left as parameters. The existing prohibition on concrete imports of
abstract modules excludes mixed groups. Refining one member creates fresh types,
retains imports of the original siblings, and does not redirect their references
back into the refinement. Whole-group refinement is deferred.

`git-issue-466-stage5.dfy` covers decreasing inherited recursion, abstract groups
and abstract imports, added invariant conjuncts, original sibling types, and
reachable false assertions. The termination attack verifies false only when the
inherited termination gate is disabled. The raw-member attack verifies false if
`BecameRaw` is disabled, while the positive control gives a body valid on raw
receivers. The invariant attacks, both inside a
group and in a refinement of an abstract member, verify false when inherited
conjunct checks are changed to assumptions. Normally their inherited witnesses
and construction sites fail. The nominal-type and mixed-group cases are separate
admission controls. Restoring each removed stage-2 admission error independently
rejects the positive control.

The site mutations can be reproduced in a disposable source build:

* Replace the resolver's `CheckGroup(group, reporter)` call with
  `Check(group.AllDeclarations(), reporter)` for the hidden-synonym attack.
* Remove the negative-result invalidation in `DeclDependsOnOwner` for the
  recursive-dependence attack.
* Use `--legacy-inherited-termination-checks` for the inherited-call attack.
* Return the original origin in `InheritedConjunctOrigin`, rather than wrapping
  it in `ForceCheckOrigin`, for both added-conjunct attacks.
* Make `BecameRaw` return false for the raw-member attack.
* Restore each removed admission branch in `RecursiveModuleGroups` separately;
  the positive stage-5 fixture must then be rejected for that branch.

Restore every mutation before the ordinary regression run. The first five
mutations must accept the corresponding `ensures false` proof; this distinguishes
an attack on the gate from an unrelated resolution or verification failure.
