ghost predicate P(i:int) { exists n:int :: n>=0 && i==n } type S = i:int | P(i) witness 0 lemma L(i:int) requires i>=0 { assert P(i); var v:S := i; }
