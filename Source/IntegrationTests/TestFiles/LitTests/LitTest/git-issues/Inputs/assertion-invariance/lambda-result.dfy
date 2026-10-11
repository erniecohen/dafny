ghost predicate P(i:int) { i>=0 } type S = i:int | P(i) witness 0 lemma L(i:int) requires i>=0 { var f:nat->S := n => i; }
