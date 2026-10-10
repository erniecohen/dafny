ghost predicate P(x:int) { x>=0 } lemma L() requires !P(-1) ensures !(exists x:int {:trigger P(x)} :: !P(x) && (forall y:int {:trigger P(y)} :: P(y))) {  }
