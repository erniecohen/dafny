// Private unexecuted helper-boundary completeness control.
// No explicit bad cast; classification and expected rejection need measurement.
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function {:abstemious} Observe(s: Outer): Impossible { s.field }
function Bad(): Outer { Outer(Bad(), Observe(Bad())) }
lemma Exploit() { var s := Bad(); assert false; }
