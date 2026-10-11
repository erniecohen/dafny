method WrongValue(m: map<bv64,bool>, k: bv64) {
  assert m[k := true][k];
  assert !m[k := true][k];
}
method NoNewKey(k: bv64, j: bv64)
  requires k != j
{
  var m := map[k := true];
  assert k in m && m[k];
  assert j in m;
}
method LiveControl(m: map<bv64,bool>, k: bv64) {
  assert m[k := true][k];
  assert false;
}
