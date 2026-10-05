// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
type Even = x: int | x % 2 == 0 witness 0
codatatype EvenStream = E(tail: EvenStream, head: Even)
function Evens(n: int): EvenStream { E(Evens(n + 1), (2 * n) as Even) }
lemma DoesNotProveFalse(n: int) {
  var s := Evens(n);
  assert s.head == 2 * n;
  assert s.tail.head == 2 * (n + 1);
  assert false;
}
