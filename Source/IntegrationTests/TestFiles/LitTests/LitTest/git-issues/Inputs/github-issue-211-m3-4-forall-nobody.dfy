// A forall statement without a body is assumed, not proved.
module Bad {
  lemma Bad()
    ensures false
  {
    forall x: int
      ensures false
  }
}
