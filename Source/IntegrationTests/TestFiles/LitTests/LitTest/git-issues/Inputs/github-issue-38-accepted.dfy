datatype S = N(o: ORDINAL) | Top
datatype Safe<T> = Safe(value: T)
type Abstract(==)
// Infinite but ordinal-safe domains remain allowed at ordinal indexing.
greatest predicate Integers(n: int) {
  exists m: int :: m < n && Integers(m)
}
least predicate SafeGeneric(n: Safe<int>) {
  forall m: Safe<int> :: SafeGeneric(m)
}
// The opposite quantifier directions do not need restricted branching.
greatest predicate GreatestForall(s: S) {
  forall t: S :: GreatestForall(t)
}
least predicate LeastExists(s: S) {
  exists t: S :: LeastExists(t)
}
// Finite branching permits ordinal-containing and abstract types.
greatest predicate Finite(xs: set<S>, s: S) {
  exists t: S :: t in xs && Finite(xs, t)
}
least predicate FiniteAbstract(xs: set<Abstract>, s: Abstract) {
  forall t: Abstract :: t in xs ==> FiniteAbstract(xs, t)
}
// A finite bound on t and an ordinal-safe type for n are independent reasons.
greatest predicate Independent(xs: set<S>, s: S) {
  exists t: S, n: int :: t in xs && n > 0 && Independent(xs, t)
}
// Moving through negation reverses quantifier polarity twice.
greatest predicate NegatedExists(s: S) {
  !(exists t: S :: !NegatedExists(t))
}
least predicate NegatedForall(s: S) {
  !(forall t: S :: !NegatedForall(t))
}
greatest predicate NonrecursiveQuantifier(s: S) {
  (exists t: S :: t.N?) && NonrecursiveQuantifier(s)
}
const OrdinarySet: set<S> := {Top}
const OrdinarySequence: seq<S> := [Top]
const OrdinaryMap: map<S, int> := map[Top := 0]
ghost predicate OrdinaryQuantifier(s: S) {
  exists t: S :: t.N? && t.o < 3
}

// Fixed-width scalars and reference identities have set-sized domains.
greatest predicate Bits(x: bv8) { exists y: bv8 :: Bits(y) }
class C { var ordinal: ORDINAL }
greatest predicate References(xs: iset<C?>, x: C?) {
  exists y: C? :: y in xs && References(xs, y)
}
