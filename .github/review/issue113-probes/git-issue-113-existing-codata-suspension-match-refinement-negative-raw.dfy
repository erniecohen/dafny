// Private unexecuted control; capture actual diagnostics before registration.
type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
function Bad():Refined { (R((match (Bad()) case R(t,f) => R(t,f)),0 as Impossible)) }
lemma Exploit() { var s := Bad(); assert false; }
