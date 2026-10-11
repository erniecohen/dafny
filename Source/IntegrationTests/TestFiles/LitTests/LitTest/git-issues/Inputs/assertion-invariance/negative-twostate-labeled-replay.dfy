// A labeled two-state call uses the label's heap for its recursive requires.
// ReplayFalse is always false, so ReplayCaller's call must be rejected.
class ReplayCell {
  var n: int
}

twostate predicate ReplayFalse(c: ReplayCell)
  requires old(c.n) >= 0
  reads c
  decreases old(c.n)
{
  if false then (ReplayCaller(c); false) else false
}

twostate lemma ReplayCallee(c: ReplayCell)
  requires old(c.n) >= 0
  requires ReplayFalse(c)
  decreases old(c.n) + 1
{
}

twostate lemma {:induction false} ReplayCaller(c: ReplayCell)
  requires old(c.n) >= 2
  requires c.n == 0
  decreases old(c.n)
{
  label ReplayCurrent:
  ReplayCallee@ReplayCurrent(c);
  assert false;
}

// This separate body checks assert-false nonvacuity without the false callee
// precondition being assumed after a failed call-precondition assertion.
twostate lemma {:induction false} DirectFalse(c: ReplayCell)
  requires old(c.n) >= 2
  requires c.n == 0
  decreases old(c.n)
{
  assert false; // Independent invalid assertion.
}

method ReachReplayCaller()
{
  var c := new ReplayCell;
  c.n := 2;
  label ReplayPrevious:
  c.n := 0;
  ReplayCaller@ReplayPrevious(c);
  DirectFalse@ReplayPrevious(c);
}
