// Private unexecuted control; capture actual diagnostics before registration.
type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
newtype RefinedN = Refined witness *
function Bad():RefinedN { (R((var s := (Bad() as Refined); R(s,s.field)),0 as Impossible) as RefinedN) }
lemma Exploit() { var s := Bad(); assert false; }
