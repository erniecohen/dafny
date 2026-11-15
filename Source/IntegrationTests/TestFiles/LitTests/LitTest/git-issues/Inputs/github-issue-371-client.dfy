module Client {
  import Concrete
  import Leaf

  lemma UseK()
    ensures Concrete.K + Leaf.B == 7
  {
    reveal Concrete.K, Leaf.B;
  }

  lemma UseLemmas(c: Concrete.Cell, b: Leaf.Box)
    ensures Concrete.Cell.S + c.I + b.J == 20
  {
    Concrete.CellIs(c);
    Leaf.BoxIs(b);
  }
}
