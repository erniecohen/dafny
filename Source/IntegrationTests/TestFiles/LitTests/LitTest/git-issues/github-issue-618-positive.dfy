// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=0 "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=1 "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=12 "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.arith.solver=2 "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --boogie /proverOpt:O:smt.bv.solver=1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Ordinary bit-vector proofs still verify under the default bit-vector solver, under the values of
// smt.bv.solver other than 2, and with other solver options whose name or value is close to the
// rejected one (https://github.com/erniecohen/dafny/issues/618).

lemma Wraps() {
  var x: bv8 := 1;
  var y: bv8 := 2;
  assert x - y == 255;
}

lemma SubtractThenAdd(x: bv16, y: bv16)
  ensures x - y + y == x
{
}

lemma ShiftDoubles(x: bv32)
  requires x < 0x1000
  ensures x << 1 == x * 2
{
}

lemma MaskBelow(x: bv64)
  ensures x & 0xff <= 0xff
{
}
