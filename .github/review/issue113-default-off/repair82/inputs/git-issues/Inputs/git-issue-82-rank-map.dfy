// An identity-key map has the same infinite lower-rank domain.
datatype Leaf = Leaf(i: int)
datatype Container = Container(children: iset<Leaf>)

ghost function InfiniteChildren(): iset<Leaf> { iset i: int | true :: Leaf(i) }

ghost function InvalidFiniteRankMap(): map<Leaf, int> {
  map d: Leaf | d < Container(InfiniteChildren()) :: d.i
}
