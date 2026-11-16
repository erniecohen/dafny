// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(value: T, next: Stream<T>)
newtype Wrap<T> = Stream<T> witness *
newtype Id<T> = T witness *
type ReferenceFreeStream(!new) = Wrap<int>

function Repeat<T>(value: T): Wrap<T> {
  Cons(value, Repeat(value) as Stream<T>) as Wrap<T>
}

lemma Head<T>(value: T)
  ensures Repeat(value).value == value
{
}

lemma FiniteActual(x: Id<Id<ReferenceFreeStream>>)
  ensures allocated(x)
{
}
