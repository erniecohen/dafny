// Private unexecuted prefix-rule preservation control.
// Both the extreme predicate and generated prefix must keep their can-call facts.
least predicate ReachesZero(n: nat) {
  n == 0 || ReachesZero(n - 1)
}
lemma AllReachZero(n: nat)
  ensures ReachesZero(n)
  decreases n
{
  if n != 0 { AllReachZero(n - 1); }
}
greatest predicate AllNonnegative(s: NatStream) {
  s.head >= 0 && AllNonnegative(s.tail)
}
codatatype NatStream = N(head: nat, tail: NatStream)
function Count(n: nat): NatStream { N(n, Count(n + 1)) }
greatest lemma CountsAreNonnegative(n: nat)
  ensures AllNonnegative(Count(n))
{
  CountsAreNonnegative(n + 1);
}
