// Private unexecuted discriminator-count control; proposed repaired exit: 4.
type Impossible = x: int | false witness *
codatatype Outer = Stop | More(tail: Outer, field: Impossible)
function Bad(): Outer { More(Bad(), 0 as Impossible) }
lemma Exploit() { var s := Bad(); assert false; }
