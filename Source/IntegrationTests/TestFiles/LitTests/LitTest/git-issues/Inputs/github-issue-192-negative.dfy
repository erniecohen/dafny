// Vacuity controls: each lemma must fail, with the option on and off.
type Neg = x: int | x < 0 witness -1
lemma RangeTypes() ensures false {
  var m1: map<bv8, nat> := map[5 := 0];
  var m2: map<bv8, Neg> := map[5 := -1];
  var a := m1[5];
  var b := m2[5];
}
lemma TwoMaps(k: bv8) {
  var m1 := map[k := 1];
  var m2 := map[k := 2];
  assert m1[k] == m2[k];
}
lemma IntBvAlias(m: map<bv8, int>, n: map<int, int>, k: bv8)
  requires k in m && (k as int) in n && m[k] != n[k as int]
  ensures false
{}
lemma MsWrong(ms: multiset<bv8>, k: bv8) ensures ms[k := 3][k] == 4 {}
lemma Control(m: map<bv8, int>, k: bv8, ms: multiset<bv8>) ensures false {
  assert m[k := 0][k] == 0 || ms[k := 1][k] == 1;
}
