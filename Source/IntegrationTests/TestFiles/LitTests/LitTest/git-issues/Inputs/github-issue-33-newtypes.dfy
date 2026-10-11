newtype BWord = b: bv32 | true witness 0

lemma BWordReverse(w: BWord)
  ensures ((w as int) as BWord) == w
{}

lemma IntoBWord(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures (a as BWord) as int == a
{}
