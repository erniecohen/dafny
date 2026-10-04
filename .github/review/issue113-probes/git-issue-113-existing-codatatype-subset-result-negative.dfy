// RUN: %exits-with 4 %verify --type-system-refresh=true "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" > "%t-old"
// Independent, unexecuted baseline probe. No newtype declaration or feature flag.
// Classify on the unchanged shipped compiler before attributing any repair to #113.

codatatype Stream = Cons(head: int, tail: Stream)
type Impossible = s: Stream | false witness *

function Bad(): Impossible {
  Cons(0, Bad() as Stream) as Impossible
}

lemma Exploit() {
  var s := Bad();
  assert false;
}
