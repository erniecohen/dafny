// Private unexecuted control; capture actual diagnostics before registration.
type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
function Bad():Refined { (R((var s := (Bad()); R(s,s.field)),0 as Impossible)) }
lemma Exploit() { var s := Bad(); assert false; }
