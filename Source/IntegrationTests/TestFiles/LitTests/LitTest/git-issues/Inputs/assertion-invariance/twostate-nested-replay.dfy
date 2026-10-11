// A nested statement expression preserves the caller's initial-heap context.
class ReplayCell {
  var n: int
}

twostate lemma ReplayHelper(c: ReplayCell)
  requires old(c.n) >= 0
  requires old(c.n) == 2
  ensures old(c.n) == 2
  decreases old(c.n)
{
  assert old(c.n) == 2;
  if false { ReplayCaller(c); }
}

twostate lemma ReplayCallee(c: ReplayCell)
  requires old(c.n) >= 0
  requires old(c.n) == 2
  requires (ReplayHelper(c); old(c.n) == 2)
  decreases old(c.n) + 1
{
}

twostate lemma {:induction false} ReplayCaller(c: ReplayCell)
  requires old(c.n) >= 4
  requires c.n == 2
  decreases old(c.n)
{
  label ReplayCurrent:
  ReplayCallee@ReplayCurrent(c);
}

method ReachReplayCaller()
{
  var c := new ReplayCell;
  c.n := 4;
  label ReplayPrevious:
  c.n := 2;
  ReplayCaller@ReplayPrevious(c);
}
