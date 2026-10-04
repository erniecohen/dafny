// RUN: %exits-with 4 %verify --type-system-refresh=true "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" > "%t-old"
// Independent unexecuted baseline probe. No newtype declaration or feature flag.

type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function Bad(): Outer {
  Outer(Bad(), 0 as Impossible)
}

lemma Exploit() {
  var s := Bad();
  assert false;
}
