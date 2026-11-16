// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream = Cons(head: int, tail: Stream)
newtype Impossible = s: Stream | false witness *
function Zeros(): Stream { Cons(0, Zeros()) }

codatatype Outer = Outer(tail: Outer, field: Impossible)
function Bad(): Outer {
  // Put the recursive argument first to expose any consequence assumption
  // before the explicit checked introduction of the impossible second field.
  Outer(Bad(), Zeros() as Impossible)
}

lemma Exploit() {
  var s := Bad();
  assert false;
}
