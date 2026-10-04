// Private unexecuted SCC control; proposed repaired exit: 4.
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function BadA(): Outer { Outer(BadB(), 0 as Impossible) }
function BadB(): Outer { Outer(BadA(), 0 as Impossible) }
lemma Exploit() { var s := BadA(); assert false; }
