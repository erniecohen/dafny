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

// Only assert statements are rewritten: a postcondition keeps today's translation.
lemma EnsuresNotRewritten()
  ensures forall x: int :: (assert P(x) by { LP(x); } P(x))
{
}

// Heap-shifting forms (review of PR 226).  Inside old, old@L, fresh and unchanged the translation
// of `o in s` has no allocation antecedent, while the well-formedness check assumes `o` allocated
// in the old heap.  The rewriter does not enter them, so each assertion below fails as it does
// without the option, and neither `ensures false` nor the `assert false` after it verifies.
class C { }

method OldBad() ensures false {
  var c := new C;
  var s: set<object> := {c};
  assert old(forall o | o in s :: (assert allocated(o); allocated(o)));
  assert false;
}

method OldAssertFalse() {
  var c := new C;
  var s: set<object> := {c};
  assert old(forall o | o in s :: (assert allocated(o); allocated(o)));
  assert false;
}

method OldAtBad() ensures false {
  label L:
  var c := new C;
  var s: set<object> := {c};
  assert old@L(forall o | o in s :: (assert allocated(o); allocated(o)));
  assert false;
}

method OldUnionBad(s0: set<object>) ensures false {
  var c := new C;
  var s := s0 + {c};
  assert old(forall o | o in s :: (assert allocated(o); allocated(o)));
  assert false;
}

twostate lemma TwoStateLemmaBad(new c: C) ensures false {
  var s: set<object> := {c};
  assert old(forall o | o in s :: (assert allocated(o); allocated(o)));
  assert false;
}

twostate predicate OldAllocated(new s: set<object>) {
  old(forall o | o in s :: (assert allocated(o); allocated(o)))
}

method TwoStateFunctionBad() ensures false {
  var c := new C;
  var s: set<object> := {c};
  assert OldAllocated(s);
  assert false;
}

method FreshBad() ensures false {
  var c := new C;
  var s: set<object> := {c};
  assert fresh(if old(forall o | o in s :: (assert allocated(o); allocated(o))) then c else c);
  assert old(allocated(c));
  assert false;
}

method UnchangedBad(s: set<object>) ensures false {
  var c := new C;
  assert unchanged(if old(forall o | o in s + {c} :: (assert allocated(o); allocated(o))) then {c} else {c});
  assert old(allocated(c));
  assert false;
}

// An assert statement inside a statement expression under old runs in the old heap; it is left
// as it is (as is every assert statement inside a statement expression).
method NestedOldBad() ensures false {
  var c := new C;
  var s: set<object> := {c};
  assert old((assert forall o | o in s :: (assert allocated(o); allocated(o)); true));
  assert false;
}
