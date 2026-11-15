// https://github.com/erniecohen/dafny/issues/363
// An application of a lambda, or of a function handle, whose parameter or result is an ORDINAL
// beta-reduces.  An ORDINAL has the Boogie type Box without being a boxed value, and the
// translation left an ORDINAL argument, an ORDINAL lambda body or an ORDINAL function result
// unboxed, while the other side of the application unboxed it.  A forall statement that assigns
// an ORDINAL field boxed some right-hand sides, such as o + 1, which the heap stores unboxed.
// Inputs/github-issue-363-negative.dfy has the controls.
// RUN: echo "positive, both resolvers" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --type-system-refresh --general-newtypes "%s" >> "%t"
// RUN: echo "controls, both resolvers" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-363-negative.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --type-system-refresh --general-newtypes "%S/Inputs/github-issue-363-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The issue's program: ORDINAL -> int, int -> ORDINAL, ORDINAL -> ORDINAL, ORDINAL -> bool.
lemma A(o: ORDINAL)
  ensures ((p: ORDINAL) => if p == o then 1 else 0)(o) == 1
{}

lemma B(o: ORDINAL)
  ensures ((x: int) => o)(0) == o
{}

lemma C(o: ORDINAL)
  ensures ((p: ORDINAL) => p)(o) == o
{}

lemma D(o: ORDINAL)
  ensures ((p: ORDINAL) => p == o)(o)
{}

function Id(p: ORDINAL): ORDINAL { p }

lemma E(o: ORDINAL)
  ensures Id(o) == o
{}

// Function handles, and lambdas passed as arguments.
function Succ(p: ORDINAL): ORDINAL { p + 1 }

function Call(f: ORDINAL -> ORDINAL, o: ORDINAL): ORDINAL { f(o) }

lemma Handles(o: ORDINAL)
  ensures Call(Id, o) == o
  ensures Call(Succ, o) == o + 1
  ensures Call((p: ORDINAL) => p, o) == o
{}

lemma Pointwise(o: ORDINAL, f: ORDINAL -> ORDINAL)
  requires forall p :: f(p) == p
  ensures f(o) == o
{}

// Preconditions, two parameters, and a lambda that returns a lambda.
lemma Partial(o: ORDINAL)
  ensures ((p: ORDINAL) requires p == o => p)(o) == o
{}

lemma TwoParameters(o: ORDINAL)
  ensures ((p: ORDINAL, q: ORDINAL) => if p < q then q else p)(o, o + 1) == o + 1
{}

lemma Curried(o: ORDINAL)
  ensures ((x: int) => (p: ORDINAL) => p)(0)(o) == o
{}

// Field assignments, in a forall statement and in an ordinary statement.
class Cell {
  var f: ORDINAL
}

method ForallAssign(s: set<Cell>, o: ORDINAL)
  modifies s
  ensures forall c :: c in s ==> c.f == o + 1
{
  forall c | c in s {
    c.f := o + 1;
  }
}

method Assign(c: Cell, o: ORDINAL)
  modifies c
  ensures c.f == o + 1
{
  c.f := o + 1;
}
