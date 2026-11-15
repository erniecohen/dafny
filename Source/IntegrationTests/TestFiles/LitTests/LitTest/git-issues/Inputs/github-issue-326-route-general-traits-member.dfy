// --general-traits datatype: a datatype member, used as a function value, that builds the datatype.

trait Tr { function Get(n: nat): Tr }
datatype D extends Tr = DLeaf | DNode(f: nat -> Tr) {
  function Get(n: nat): Tr { DNode(this.Get) }
}
lemma Bad(d: D) ensures false { assert d.Get(0) == DNode(d.Get); }
