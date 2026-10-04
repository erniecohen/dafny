// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft: codata nominality and compiled-equality restrictions persist.

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
