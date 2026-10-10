ghost predicate P(i:int) { i>=0 } type S = i:int | P(i) witness 0 datatype D = D(value:S) lemma L(i:int) requires i>=0 { var d := D(i); }
