// Must keep failing with --statement-expression-conclusions (github-issue-96.dfy).
ghost predicate P(x: int)
lemma LP(x: int) ensures P(x)

// The statement is still checked: the inner assertion fails (line 7).
lemma InnerChecked() {
  assert forall n: nat :: (assert n > 5; true);
}

// The conclusion is only an antecedent: a false body still fails (line 12).
lemma BodyChecked() {
  assert forall x: int :: (assert P(x) by { LP(x); } P(x) && x > 0);
}

// Vacuity: with conclusions in play under quantifiers, `ensures false` still fails (line 18).
lemma VacuityPost()
  ensures false
{
  assert forall x: int :: (assert P(x) by { LP(x); } P(x));
  assert forall n: nat :: (assert n >= 0; n + 1 > 0);
}

// Vacuity: a binder whose range implies allocation, so that its translation has no
// allocation antecedent; `assert false` still fails (line 27).
method VacuityAlloc(s: set<object>) {
  assert forall o | o in s :: (assert allocated(o); true);
  assert false;
}

// The statement is checked under the guard only, and its conclusion is used only there:
// the second assertion fails (line 34).
lemma Guard() {
  assert forall n: int :: n > 5 ==> (assert n > 3; n > 3);
  assert forall n: int :: n > 5 ==> (assert n > 3; n > 7);
}

// A lambda body is left as it is (line 40).
lemma Lambda() {
  var f := (x: int) => (assume false; false);
  assert f(0);
}
