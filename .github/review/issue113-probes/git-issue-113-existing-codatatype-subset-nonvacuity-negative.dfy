// RUN: %exits-with 4 %verify --type-system-refresh=true "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" > "%t-old"
// Independent unexecuted baseline consistency control. No newtype/feature flag.

codatatype Stream = Cons(head: int, tail: Stream)
type Trivial = s: Stream | true witness *
function Raw(): Stream { Cons(1, Raw()) }
function Wrapped(): Trivial { Cons(1, Wrapped() as Stream) as Trivial }
lemma NonVacuity() {
  assert Raw().head == 1;
  assert Wrapped().head == 1;
  assert false;
}
