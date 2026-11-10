// Vacuity controls for Std.RealCompleteness (#205), run by ../github-issue-205.dfy.
// Each lemma makes a live call of the trusted Complete, and its assert false must
// fail: on a bounded nonempty set, on the empty set, on an unbounded set, and with
// a witness that is not a member of the set.

import opened Std.RealCompleteness

lemma BoundedVacuity()
{
  var s := iset{0.0};
  assert upper(s, 1.0);
  var u := Complete(s, 0.0, 1.0);
  assert upper(s, u);
  assert upper(s, 0.0);
  assert u == 0.0;
  assert false; // error
}

lemma EmptyVacuity()
{
  var s: iset<real> := iset{};
  var u := Complete(s, 0.0, 0.0);
  assert !(0.0 in s);
  assert upper(s, u - 1.0);
  assert false; // error
}

lemma UnboundedVacuity()
{
  var s := iset x: real {:nowarn} | true;
  var u := Complete(s, 0.0, 0.0);
  assert 0.0 in s;
  assert 1.0 in s;
  assert u + 1.0 in s;
  assert !upper(s, 0.0);
  assert false; // error
}

lemma BadWitnessVacuity()
{
  var s := iset{0.0};
  assert upper(s, 1.0);
  var u := Complete(s, 5.0, 1.0);
  assert !(5.0 in s);
  assert false; // error
}

// Sup is total: on an ineligible set it is 0.0 and supplies nothing more.
lemma SupOfIneligible()
{
  var s: iset<real> := iset{};
  assert !Eligible(s);
  assert Sup(s) == 0.0;
  assert false; // error
}
