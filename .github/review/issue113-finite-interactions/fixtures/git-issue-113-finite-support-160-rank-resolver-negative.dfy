// Private source-only draft; intended finite-bound resolution rejection.
// Rank is allocation-independent, but is not itself a finite witness bound.
datatype Leaf = Leaf(value: int)
datatype Container = Container(children: iset<Leaf>)
newtype WrappedContainer = Container witness *

// Intended feature-on negative, independently of AX.
ghost function InvalidFiniteRanks(c: WrappedContainer): set<Leaf> {
  set d: Leaf | d < c
}

// This is the unchanged-language PR160 counterpart, not a feature failure.
ghost function RawInvalidFiniteRanks(c: Container): set<Leaf> {
  set d: Leaf | d < c
}
