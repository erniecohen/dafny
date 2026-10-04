// Private unexecuted instantiated generic refinement control; expected rejection.
type Impossible = x: int | false witness *
codatatype Outer<T> = Outer(tail: Outer<T>, field: T)
function Bad(): Outer<Impossible> { Outer(Bad(), 0 as Impossible) }
lemma Exploit() { var s := Bad(); assert false; }
