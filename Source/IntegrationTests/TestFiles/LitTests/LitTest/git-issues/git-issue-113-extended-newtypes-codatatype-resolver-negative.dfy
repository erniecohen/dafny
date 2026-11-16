// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream = Cons(head: int, tail: Stream)
newtype Left = Stream witness *
newtype Right = Stream witness *

method NominalAssignment(x: Left) {
  var y: Stream := x;
  var z: Right := x;
}

method CompiledEquality(x: Left, y: Left) {
  var same := x == y;
}

lemma MixedNominals(x: Left, y: Right) {
  assert x == y;
  assert x ==#[1] y;
}

function {:abstemious} DeepDestructor(s: Left): Left {
  Cons(s.tail.head, s.tail) as Left
}

function {:abstemious} ForbiddenEquality(s: Left, t: Left): Left {
  if s == t then s else t
}
