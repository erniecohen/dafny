// A labeled two-state call uses the label's heap for its recursive requires.
// The nested statement-expression requires is false, so the call must fail.
class ReplayCell {
  var n: int
}

twostate lemma ReplayHelper(c: ReplayCell)
  requires old(c.n) >= 0
  decreases old(c.n)
{
  assert old(c.n) >= 0;
  if false { ReplayCaller(c); }
}

twostate lemma ReplayCallee(c: ReplayCell)
  requires old(c.n) >= 0
  requires (ReplayHelper(c); false)
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
