// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Repeat is universally productive for every input and scans a legal recursive co-family.
codatatype Stream<T> = Cons(value: T, next: Stream<T>)
newtype Wrap<T> = Stream<T>
newtype Id<T> = T
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
