// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Independent unexecuted probe: an acyclic nominal field must not bootstrap membership.

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
