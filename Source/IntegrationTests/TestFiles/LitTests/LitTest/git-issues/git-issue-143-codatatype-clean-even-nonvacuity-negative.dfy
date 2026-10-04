// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
type Even = x: int | x % 2 == 0 witness 0
codatatype EvenStream = Cons(head: Even, tail: EvenStream)
function EvenValues(n: Even): EvenStream { Cons(n, EvenValues((n + 2) as Even)) }
lemma Head(n: Even)
  ensures EvenValues(n).head == n
{}
lemma InhabitedControl() {
  var value := EvenValues(0);
  assert value.head == 0;
  assert value.head % 2 == 0;
  assert false; // Only the final assert false is intended to fail.
}
