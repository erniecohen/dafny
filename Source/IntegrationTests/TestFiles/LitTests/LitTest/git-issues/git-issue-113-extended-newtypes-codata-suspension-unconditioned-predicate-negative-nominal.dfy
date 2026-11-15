// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
function Repeat<T>(x:T):Wrapped<T> { (Cons(x, (Repeat(x) as Stream<T>)) as Wrapped<T>) }
newtype PositiveHead = s:Stream<int> | s.head>0 witness *
method BadCast(s:Stream<int>) { var p := s as PositiveHead; }
