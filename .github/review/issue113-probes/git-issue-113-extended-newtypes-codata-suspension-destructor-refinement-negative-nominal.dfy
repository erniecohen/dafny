// Private unexecuted control; capture actual diagnostics before registration.
type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
newtype RefinedN = Refined witness *
function Bad():RefinedN { (R(R((Bad() as Refined), (Bad() as Refined).field), (Bad() as Refined).field) as RefinedN) }
lemma Exploit() { var s := Bad(); assert false; }
