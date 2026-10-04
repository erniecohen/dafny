method Test(m0: map<nat,bool>, m1: map<bv64,bool>) {
  assert m0[0 := true][0];
  assert m1[0 := true][0];
}
