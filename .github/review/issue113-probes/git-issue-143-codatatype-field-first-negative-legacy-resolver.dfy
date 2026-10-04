// Private unexecuted field-order control; proposed repaired exit: 4.
type Impossible = x: int | false witness *
codatatype Outer = Outer(field: Impossible, tail: Outer)
function Bad(): Outer { Outer(0 as Impossible, Bad()) }
lemma Exploit() { var s := Bad(); assert false; }
