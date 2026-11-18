module Client {
  import Leaf

  lemma UseLeaf()
    ensures Leaf.Double(3) == 6
  {
    reveal Leaf.Double();
  }

  lemma UseLemma()
    ensures Leaf.Double(Leaf.Double(1)) == 4
  {
    Leaf.DoubleTwice(1);
  }
}
