// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
codatatype Stream<T> = Cons(tail: Stream<T>, head: T := tail.head)
function Bad<T>(): Stream<T> { Cons(Cons(Bad<T>())) }
type Impossible = x: int | false witness *
lemma Exploit() { var value := Bad<Impossible>(); assert false; }
