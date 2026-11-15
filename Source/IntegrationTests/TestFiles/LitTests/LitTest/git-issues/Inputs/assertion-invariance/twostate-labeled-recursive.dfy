// The recursive predicate, caller and callee share a recursive component.
// Predicate depth recursion is real; its caller-expression arm is unreachable.
class ReplayValueCell {
  var n: int
}

twostate predicate ReplayValue(c: ReplayValueCell, depth: nat, expected: int)
  requires old(c.n) >= 0
  reads c
  decreases old(c.n), depth
{
  if false then (ReplayValueCaller(c); false)
  else if depth == 0 then old(c.n) == expected
  else ReplayValue(c, depth - 1, expected)
}

twostate lemma ReplayValueCallee(c: ReplayValueCell)
  requires old(c.n) >= 0
  requires ReplayValue(c, 1, 2)
  decreases old(c.n) + 1
{
}

twostate lemma {:induction false} ReplayValueCaller(c: ReplayValueCell)
  requires old(c.n) >= 4
  requires c.n == 2
  decreases old(c.n)
{
  label ReplayCurrent:
  // This predicate uses the label's old value 2 and the caller measure 4.
  // The callee measure is 3, also strictly below the caller's old measure.
  ReplayValueCallee@ReplayCurrent(c);
}

method ReachReplayValueCaller()
{
  var c := new ReplayValueCell;
  c.n := 4;
  label ReplayPrevious:
  c.n := 2;
  ReplayValueCaller@ReplayPrevious(c);
}
