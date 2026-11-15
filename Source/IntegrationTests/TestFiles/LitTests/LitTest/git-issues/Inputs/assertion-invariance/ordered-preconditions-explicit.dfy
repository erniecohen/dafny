lemma Use(i:int) requires i!=0 requires 10/i>0 {} lemma L() { assert 1!=0 && 10/1>0; Use(1); }
