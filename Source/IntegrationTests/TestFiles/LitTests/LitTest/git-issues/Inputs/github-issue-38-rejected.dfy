datatype S = N(o: ORDINAL) | Top
datatype Box<T> = Box(value: T)
datatype Chain = End | Link(next: Chain, value: Box<S>)
codatatype Stream = More(value: Box<S>, tail: Stream)
type Alias = Box<S>
type Sub = x: Alias | true witness Box(Top)
type Abstract(==)

greatest predicate Greatest(s: S) {
  exists t: S :: t.N? && (s.Top? || t.o < s.o) && Greatest(t)
}
least predicate Least(s: S) {
  forall t: S :: (t.N? && (s.Top? || t.o < s.o)) ==> Least(t)
}
greatest predicate GenericNested(s: Box<S>) {
  exists t: Box<S> :: GenericNested(t)
}
greatest predicate RecursiveNested(s: Chain) {
  exists t: Chain :: RecursiveNested(t)
}
greatest predicate Codata(s: Stream) {
  exists t: Stream :: Codata(t)
}
greatest predicate Synonym(s: Alias) {
  exists t: Alias :: Synonym(t)
}
greatest predicate Subset(s: Sub) {
  exists t: Sub :: Subset(t)
}
greatest predicate Parameter<T(!new)>(s: T) {
  exists t: T :: Parameter(t)
}
greatest predicate AbstractType(s: Abstract) {
  exists t: Abstract :: AbstractType(t)
}
least predicate UniversalIset(s: S) {
  forall t: S :: (t in (iset u: S | true) && t.N?) ==> UniversalIset(t)
}
// One finite variable does not bound another ordinal-sized variable.
greatest predicate Independent(xs: set<S>, s: S) {
  exists t: S, u: S :: t in xs && Independent(xs, u)
}
greatest predicate NegativeForall(s: S) {
  !(forall t: S :: !NegativeForall(t))
}
least predicate NegativeExists(s: S) {
  !(exists t: S :: !NegativeExists(t))
}
