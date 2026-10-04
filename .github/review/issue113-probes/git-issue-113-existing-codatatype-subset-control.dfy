// RUN: %verify --type-system-refresh=true "%s" > "%t"
// RUN: %verify --type-system-refresh=false --general-newtypes=false "%s" > "%t-old"
// Independent unexecuted baseline control. No newtype declaration or feature flag.

codatatype Stream = Cons(head: int, tail: Stream)
type Trivial = s: Stream | true witness *

function Raw(): Stream { Cons(1, Raw()) }
function Wrapped(): Trivial { Cons(1, Wrapped() as Stream) as Trivial }

lemma Observation() {
  assert Raw().head == 1;
  assert Raw().tail.head == 1;
  assert Wrapped().head == 1;
}
