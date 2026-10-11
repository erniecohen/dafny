method Width0(m: map<bv0,bool>, k: bv0) {
  assert m[0 := true][0];
  assert m[k := true][k];
  assert k in m[k := true];
}

method Width1(m: map<bv1,bool>, k: bv1) {
  assert m[0 := true][0];
  assert m[k := true][k];
  assert k in m[k := true];
}

method Width8(m: map<bv8,bool>, k: bv8) {
  assert m[0 := true][0];
  assert m[k := true][k];
  assert k in m[k := true];
}

method Width32(m: map<bv32,bool>, k: bv32) {
  assert m[0 := true][0];
  assert m[k := true][k];
  assert k in m[k := true];
}

method Width64(m: map<bv64,bool>, k: bv64) {
  assert m[0 := true][0];
  assert m[k := true][k];
  assert k in m[k := true];
}

method Width128(m: map<bv128,bool>, k: bv128) {
  assert m[0 := true][0];
  assert m[k := true][k];
  assert k in m[k := true];
}

method OtherKeys(m: map<bv64,int>, k: bv64, j: bv64)
  requires k != j && j in m
{
  assert m[k := 5][j] == m[j];
  assert m[k := 5][k] == 5;
  assert j in m[k := 5];
}
method Infinite(m: imap<bv64,bool>, k: bv64) {
  assert m[k := true][k];
  assert m[0 := true][0];
}
method NonMap(s: seq<int>, a: array<int>, k: bv8)
  requires (k as int) < |s| && (k as int) < a.Length
{
  assert s[k] == s[k as int];
  assert a[k] == a[k as int];
}
method Unchanged(m: map<nat,bool>, n: map<nat,bv64>) {
  assert m[0 := true][0];
  assert n[0 := 0][0] == 0;
}
