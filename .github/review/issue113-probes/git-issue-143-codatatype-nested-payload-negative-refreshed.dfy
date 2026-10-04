// Private unexecuted ordinary payload wrapper control; proposed repaired exit: 4.
type Impossible = x: int | false witness *
datatype Payload = P(value: Impossible)
codatatype Outer = Outer(tail: Outer, field: Payload)
function Bad(): Outer { Outer(Bad(), P(0 as Impossible)) }
lemma Exploit() { var s := Bad(); assert false; }
