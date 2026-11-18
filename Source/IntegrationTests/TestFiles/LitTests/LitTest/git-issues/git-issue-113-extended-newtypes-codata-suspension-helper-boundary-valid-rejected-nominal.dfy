// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
function {:abstemious} Carry(s:Wrapped<int>):Wrapped<int> { (Cons(1, (s as Stream<int>)) as Wrapped<int>) }
function HelperBoundary():Wrapped<int> { (Cons(1, (Carry(HelperBoundary()) as Stream<int>)) as Wrapped<int>) }
