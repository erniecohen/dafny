// A constructor over an infinite iset forces infinitely many lower ranks.
datatype Leaf = Leaf(i: int)
datatype Container = Container(children: iset<Leaf>)

ghost function InfiniteChildren(): iset<Leaf> { iset i: int | true :: Leaf(i) }

ghost function InvalidFiniteRanks(): set<Leaf> {
  set d: Leaf | d < Container(InfiniteChildren())
}
