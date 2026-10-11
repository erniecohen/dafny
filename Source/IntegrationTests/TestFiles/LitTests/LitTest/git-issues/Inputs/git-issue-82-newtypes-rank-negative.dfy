datatype Leaf = Leaf(i: int)
datatype Container = Container(children: iset<Leaf>)
newtype WrappedLeaves = s: set<Leaf> | true

ghost function UnboundedWrappedRanks(c: Container): WrappedLeaves {
  set d: Leaf | d < c
}
