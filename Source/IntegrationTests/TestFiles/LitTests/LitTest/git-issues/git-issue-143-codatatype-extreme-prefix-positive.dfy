// RUN: %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
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
