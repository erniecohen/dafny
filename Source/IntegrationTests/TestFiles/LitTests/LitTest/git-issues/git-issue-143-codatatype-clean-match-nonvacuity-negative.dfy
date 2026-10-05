// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
codatatype Stream = Cons(head: nat, tail: Stream)
function Values(n: nat): Stream { Cons(n, Values(n + 1)) }
lemma Head(n: nat) ensures Values(n).head == n {}
lemma Inhabited() { var value := Values(0); var Cons(h, t) := value; assert h == 0; assert false; }
