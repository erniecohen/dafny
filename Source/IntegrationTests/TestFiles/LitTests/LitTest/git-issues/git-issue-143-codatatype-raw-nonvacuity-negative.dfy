// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
codatatype NatStream = N(head: nat, tail: NatStream)
function Count(n: nat): NatStream { N(n, Count(n + 1)) }
lemma DoesNotProveFalse(n: nat) {
  var s := Count(n);
  assert s.head == n;
  assert s.tail.head == n + 1;
  assert false;
}
