// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.off.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.on.expect" "%t"

type Impossible = x:int | false witness *
codatatype Refined = R(tail:Refined,field:Impossible)
function Bad():Refined { (R((match (Bad()) case R(t,f) => R(t,f)),0 as Impossible)) }
lemma Exploit() { var s := Bad(); assert false; }
