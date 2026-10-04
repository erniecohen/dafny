// Private, unexecuted control. The helper returns a co-constructor, unlike the
// resolver-invalid Observe(s):Impossible control. Intended verifier rejection.
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function {:abstemious} Carry(s: Outer): Outer { Outer(s, s.field) }
function Bad(): Outer { Outer(Carry(Bad()), 0 as Impossible) }
lemma Exploit() { var s := Bad(); assert false; }
