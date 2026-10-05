ghost predicate P(i:int) { i>=0 } type S = i:int | P(i) && i%2==0 witness 0 lemma L(i:int) requires i>=0 && i%2==0 { assert P(i) && i%2==0; var v:S := i; }
