lemma ZeroWord(x: bv0)
  ensures x + x == x
  ensures !x == x
  ensures (x as int) == 0
{}
