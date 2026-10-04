// Private, unexecuted control without an explicit bad cast in Bad.
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function {:abstemious} Carry(s: Outer): Outer { Outer(s, s.field) }
function Bad(): Outer {
  Outer(Outer(Bad(), Carry(Bad()).field), Carry(Bad()).field)
}
lemma Exploit() { var s := Bad(); assert false; }
