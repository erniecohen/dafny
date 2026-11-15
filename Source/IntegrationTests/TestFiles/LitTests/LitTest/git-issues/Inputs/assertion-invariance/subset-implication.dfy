type S = i:int | i>=0 ==> i%2==0 witness -1 lemma L(i:int) requires i<0 { var v:S := i; }
