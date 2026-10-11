lemma L(f:int-->int, i:int) requires f.requires(i) { assert f.requires(i); var v := f(i); }
