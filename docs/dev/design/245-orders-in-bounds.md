# Orders in modular-termination bounds (issue 245, M4)

https://github.com/erniecohen/dafny/issues/245 adds decreases components `e by O`, where `O` is a value of
the trait `Std.Termination.Orders.Wfo<T>`.  Milestone M4 allows them in the `decreases {:global}` and
`decreases {:callback g}` bounds of `--modular-termination`
(https://github.com/erniecohen/dafny/issues/211).

## Loading the orders library

`--modular-termination` rejects the bundled standard library, since it carries no modular termination
record.  The orders library is loaded on its own under `--modular-termination` and `--decreases-orders`,
including with `--standard-libraries:false`.  It is *verified* with the program, under the program's
options, instead of being admitted by a record, so it adds nothing to the trusted base.

## What the code of a bound with an order reaches

M4a requires that a bound reach no open dispatch.  A component `e by O` runs:

- the code of `O`, an ordinary closed expression;
- the implied calls of `Wfo.gt` and `Wfo.aha` (R3 of M2).  These go to every override of those members
  except the library's `WfoOf`.  The only code `WfoOf` runs is the relation that it holds.  That relation
  was applied where the `WfoOf` was made, inside the code of `O`.

The library's `Of`, `WfoOf`, `wellFounded` and `stops` count as applications of their arguments of arrow
type.  `Chain` and `WfoOf` are marked `{:termination_closed}`.

## The binder rule

Every proof that a relation is well-founded applies chains `c: nat -> T`.  Before M4, the analysis
counted every application of a function value as unknown code, so no order could be used in a bound.

**Rule.**  An application `c(args)` is not an edge to unknown code only when all three of these hold:

1. `c` is a variable bound by a `forall` or `exists` expression, or by a `forall` statement in ghost
   code;
2. the type of `c` is a total arrow `A -> B`, not `A --> B` or `A ~> B`;
3. the application is in a ghost context.  A quantifier over an arrow type has no bounds, so the resolver
   accepts one only in a ghost context.

Every other application is still unknown code.  That covers parameters, locals, fields, results, and
lambdas formed in the program and bound to a variable.  A binder may not be passed for a parameter that
has a callback bound.

**Why it is sound.**  M4a exists because a bound whose computation reaches open dispatch makes
termination circular: the descent of the callee is measured against the very bound being defined.  A
variable bound by a quantifier ranges over every value of its type.  When the arrow is total, that set
exists before the function being defined.  So `forall c :: ... c(n) ...` is a formula over existing
functions, and nothing evaluates `c` or calls through it.  One of those values may be a lambda that calls
the function being defined, but that does not make the definition recursive.  Open dispatch is a risk for
function values that the program computes.  It is not a risk for binders.

Skipping lemma calls in the reachability walk was considered and rejected.  A lemma reached from a bound
has the same circularity: the lemma's termination may be proved with dispatch rules against that bound,
and the bound's well-definedness relies on the lemma's postcondition.

**Consequences.**

- A well-foundedness proof used in a bound binds each chain with a quantifier.  For example,
  `lemma Climbs(n: nat) ensures forall c: Chain<nat> :: ... ==> c(n) >= n` instead of
  `lemma Climbs(c: Chain<nat>, n: nat)`.
- The `aha(c)` of a user implementation of `Wfo` applies a parameter, so it is unknown code, and a bound
  with an order then reaches it.  Under `--modular-termination`, build orders with `Of`.

## Tests

`git-issues/github-issue-245.dfy`, with inputs `github-issue-245-mt*.dfy`:

- **Positive:** a global bound in the order's own module; global and callback bounds in a client of
  another module; the library verified with the client.
- **Verification errors:** the same bound; the wrong direction; another order expression; a callback bound
  that is not below.
- **Resolution errors:**
  - a bound that applies a parameter;
  - a bound that applies a lambda bound to a variable;
  - an order whose proof passes the chain to a lemma parameter;
  - an order whose lemma quantifies over `nat ~> nat`;
  - an order function that is not `{:termination_closed}`, used from another module.
