// Multiset multiplicity lookups with bitvector elements (https://github.com/erniecohen/dafny/issues/192).
lemma MsSingleton(k: bv8) ensures multiset{k}[k] == 1 {}
lemma MsUpdate(ms: multiset<bv8>, k: bv8) ensures ms[k := 3][k] == 3 {}
lemma MsMember(k: bv8) ensures k in multiset{k} {}
lemma MsUnion(ms: multiset<bv8>, k: bv8) ensures (ms + multiset{k})[k] == ms[k] + 1 {}
lemma MsW64(ms: multiset<bv64>, k: bv64) ensures ms[k := 3][k] == 3 {}
lemma MsInt(ms: multiset<int>, k: int) ensures ms[k := 3][k] == 3 {}
lemma MsSeq(k: bv8) ensures multiset([k, k])[k] == 2 {}
lemma MsNonNeg(ms: multiset<bv8>, k: bv8) ensures ms[k] >= 0 {}
lemma MsOther(ms: multiset<bv8>, k: bv8, j: bv8) requires j != k ensures ms[k := 3][j] == ms[j] {}
