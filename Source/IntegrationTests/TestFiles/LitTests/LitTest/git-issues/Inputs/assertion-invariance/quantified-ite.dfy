ghost predicate P(x:int) { x>=0 } lemma L() requires P(0) ensures if (exists x:int {:trigger P(x)} :: P(x)) then (exists y:int {:trigger P(y)} :: P(y)) else false {  }
