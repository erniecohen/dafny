// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
codatatype NatStream = Cons(head: nat, tail: NatStream)
function NatValues(n: nat): NatStream { Cons(n, NatValues(n + 1)) }
lemma Head(n: nat)
  ensures NatValues(n).head == n
{}
lemma InhabitedControl() {
  var value := NatValues(7);
  assert value.head == 7;
  assert false; // Only the final assert false is intended to fail.
}
