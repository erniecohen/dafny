// Verifies only when Boogie unrolls the loop instead of using its invariant.
module Bad {
  lemma Bad() ensures 1 == 2 {
    var i := 0;
    while i < 10
      invariant 0 <= i <= 10
    {
      i := i + 1;
    }
  }
}
