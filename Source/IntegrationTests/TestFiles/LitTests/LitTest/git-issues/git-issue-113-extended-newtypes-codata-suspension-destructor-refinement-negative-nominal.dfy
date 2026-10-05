// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
newtype RefinedN = Refined witness *
function Bad():RefinedN { (R(R((Bad() as Refined), (Bad() as Refined).field), (Bad() as Refined).field) as RefinedN) }
lemma Exploit() { var s := Bad(); assert false; }
