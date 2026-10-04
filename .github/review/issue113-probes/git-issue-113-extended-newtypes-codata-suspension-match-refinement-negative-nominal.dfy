// Private unexecuted control; capture actual diagnostics before registration.
type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
newtype RefinedN = Refined witness *
function Bad():RefinedN { (R((match (Bad() as Refined) case R(t,f) => R(t,f)),0 as Impossible) as RefinedN) }
lemma Exploit() { var s := Bad(); assert false; }
