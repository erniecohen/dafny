opaque predicate P(i:int) { i>=0 } lemma L() { reveal P(); hide P; assert P(-1); }
