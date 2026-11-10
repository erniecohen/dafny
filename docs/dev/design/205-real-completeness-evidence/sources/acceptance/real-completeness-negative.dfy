// Each method must fail at its single assert false, after a live primitive call.
include "real-completeness.dfy"

lemma boundedVacuity()
{
  var s := iset{0.0};
  assert upper(s,1.0);
  var u := complete(s,0.0,1.0);
  assert upper(s,u);
  assert upper(s,0.0);
  assert u == 0.0;
  assert false;
}

lemma emptyVacuity()
{
  var s: iset<real> := iset{};
  var u := complete(s,0.0,0.0);
  assert !(0.0 in s);
  assert upper(s,u-1.0);
  assert false;
}

lemma unboundedVacuity()
{
  var s := iset x: real {:nowarn} | true;
  var u := complete(s,0.0,0.0);
  assert 0.0 in s;
  assert 1.0 in s;
  assert u+1.0 in s;
  assert !upper(s,0.0);
  assert false;
}
