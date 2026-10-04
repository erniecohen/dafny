// Private unexecuted control; capture actual diagnostics before registration.
type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
function Bad():Refined { (R(R((Bad()), (Bad()).field), (Bad()).field)) }
lemma Exploit() { var s := Bad(); assert false; }
