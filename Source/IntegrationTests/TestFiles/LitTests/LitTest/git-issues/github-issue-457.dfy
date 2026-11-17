// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %verify --legacy-allocated-in-lambdas "%S/Inputs/github-issue-457-legacy.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-457-positive.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-457-positive.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/457): a lambda expression in a method could
// mention `allocated(c)`.  Its body is translated in the heap the lambda is applied in, so its value
// depended on that heap's allocation state, while the frame axiom of its arrow equates its values in
// any two heaps that agree on its (here empty) reads frame.  Together they proved false.  Such a use
// is now a resolution error, under both resolvers, in the body, the precondition and the reads clause
// of a lambda, unless it is inside an `old` expression within that lambda (an `old` around the lambda
// does not help: the body is still evaluated in the heap of the application).  The positive and
// control cases are in Inputs/github-issue-457-positive.dfy.  Under the hidden
// --legacy-allocated-in-lambdas, the reproducer still verifies (Inputs/github-issue-457-legacy.dfy).

class C { constructor () {} }

method Reproducer() ensures false {
  var c := new C();
  ghost var f: int -> bool := (x: int) => allocated(c);  // error
  ghost var f0: int ~> bool := f;
  ghost var b := f0(0);
  assert b;
}

method InPrecondition(c: C) {
  ghost var f := (x: int) requires allocated(c) => true;  // error
}

method InReadsClause(c: C) {
  ghost var f := (x: int) reads if allocated(c) then {c} else {} => true;  // error
}

method LambdaInsideOld() ensures false {
  var c := new C();
  ghost var f: int -> bool := old((x: int) => allocated(c));  // error
  ghost var f0: int ~> bool := f;
  ghost var b := f0(0);
  assert b;
}

method InnerLambdaInsideOld(c: C) {
  ghost var f := (x: int) => old((y: int) => allocated(c));  // error: the inner lambda resets `old`
}

method NestedInBody(c: C) {
  ghost var f := (x: int) => var s := {c}; forall d <- s :: allocated(d) || x == 0;  // error
}
