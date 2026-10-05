// Universally quantify the legal acyclic nominal stream carrier.
// Lambda permissions must retain the exact source family and formal types.
codatatype Stream<T> = Cons(head: T, tail: Stream<T>)
newtype Wrapped<T> = Stream<T> witness *

ghost function HeadThunk<T(!new)>(s: Wrapped<T>): () -> T {
  () => s.head
}

ghost function MappedHeads<T(!new)>(s: Wrapped<T>): map<bool, () -> T> {
  map b: bool | true :: b := (() => if b then s.head else s.tail.head)
}

lemma UniversalThunks<T(!new)>(s: Wrapped<T>)
  ensures HeadThunk(s)() == s.head
  ensures true in MappedHeads(s) && false in MappedHeads(s)
  ensures MappedHeads(s)[true]() == s.head
  ensures MappedHeads(s)[false]() == s.tail.head
{}
