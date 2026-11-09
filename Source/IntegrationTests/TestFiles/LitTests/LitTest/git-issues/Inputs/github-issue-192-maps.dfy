// The programs of https://github.com/erniecohen/dafny/issues/192, and other key widths.
lemma CheckBv(m: map<bv32, seq<int>>, k: bv32, v: seq<int>) ensures m[k := v][k] == v {}
lemma CheckBvInt(m: map<bv32, int>, k: bv32, v: int) ensures m[k := v][k] == v {}
lemma CheckInt(m: map<int, seq<int>>, k: int, v: seq<int>) ensures m[k := v][k] == v {}
lemma CheckBv8(m: map<bv8, int>, k: bv8, v: int) ensures m[k := v][k] == v {}
lemma CheckBvMem(m: map<bv32, int>, k: bv32, v: int) ensures k in m[k := v] {}
lemma W0(m: map<bv0, int>, k: bv0, v: int) ensures m[k := v][k] == v {}
lemma W1(m: map<bv1, int>, k: bv1, v: int) ensures m[k := v][k] == v {}
lemma W64(m: map<bv64, int>, k: bv64, v: int) ensures m[k := v][k] == v {}
lemma W300(m: map<bv300, int>, k: bv300, v: int) ensures m[k := v][k] == v {}
lemma IMapW8(m: imap<bv8, int>, k: bv8, v: int) ensures m[k := v][k] == v {}
