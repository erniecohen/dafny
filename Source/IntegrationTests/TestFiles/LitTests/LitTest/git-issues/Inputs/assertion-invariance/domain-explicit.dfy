lemma L(m: map<int,int>, i: int) requires i in m { assert i in m; var v := m[i]; }
