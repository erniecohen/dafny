// Private unexecuted regression input.
// Corresponding measured raw5c case verified falsely; proposed repaired exit: 4.
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function Bad(): Outer { Outer(Bad(), 0 as Impossible) }
lemma Exploit() { var s := Bad(); assert false; }
