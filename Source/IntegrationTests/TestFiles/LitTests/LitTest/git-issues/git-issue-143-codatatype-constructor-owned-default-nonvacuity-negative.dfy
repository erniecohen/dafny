// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
codatatype Stream = Cons(tail: Stream, head: nat := tail.head)
function Values(n: nat): Stream { Cons(Cons(Values(n + 2), n + 1)) }
lemma Head(n: nat) ensures Values(n).head == n + 1 {}
lemma Inhabited() { var value := Values(0); assert value.head == 1; assert false; }
