// Private unexecuted inhabited, executable refinement control; expected rejection.
codatatype Outer = Outer(tail: Outer, field: nat)
function Bad(): Outer { Outer(Bad(), -1) }
lemma Exploit() { var s := Bad(); assert false; }
