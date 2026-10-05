ghost predicate P(i:int) { i>=0 } lemma Use(i:int) requires P(i) {} lemma L(i:int) requires i>=0 { Use(i); }
