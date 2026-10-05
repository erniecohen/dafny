// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
codatatype RawStream = Raw(head: int, tail: RawStream)
function RawValues(n: int): RawStream { Raw(n, RawValues(n + 1)) }
lemma Head(n: int)
  ensures RawValues(n).head == n
{}
lemma InhabitedControl() {
  var value := RawValues(0);
  assert value.head == 0;
  assert false; // Only the final assert false is intended to fail.
}
