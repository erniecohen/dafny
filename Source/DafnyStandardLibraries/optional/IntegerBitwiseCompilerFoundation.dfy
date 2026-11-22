// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Independent raw-reference agreement proofs; no consumer contracts are included.
// Requires --integer-bitwise.
include "IntegerBitwise.dfy"

module Std.Arithmetic.IntegerBitwiseCompilerFoundation {
  import R = IntegerBitwise

  lemma {:induction false} Power(n: nat)
    ensures IntegerBitwiseModel.P(n) == R.P(n)
    decreases n
  {
    IntegerBitwiseModel.unfoldP(n);
    reveal R.P();
    if n > 0 { Power(n-1); }
  }
  lemma {:induction false} Normalize(n: nat, a: int)
    ensures IntegerBitwiseModel.norm(n,a) == R.norm(n,a)
  {
    Power(n);
    IntegerBitwiseModel.unfoldNorm(n,a);
  }
  lemma {:induction false} And(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.andNat(n,a,b) == R.andNat(n,a,b)
    decreases n
  {
    IntegerBitwiseModel.unfoldAnd(n,a,b);
    reveal R.andNat();
    if n > 0 { And(n-1,a/2,b/2); }
  }
  lemma {:induction false} Not(n: nat, a: int)
    ensures IntegerBitwiseModel.notNat(n,a) == R.notNat(n,a)
  {
    Power(n); Normalize(n,a);
    IntegerBitwiseModel.unfoldNot(n,a);
    reveal R.notNat();
  }
  lemma {:induction false} Or(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.orNat(n,a,b) == R.orNat(n,a,b)
  {
    Not(n,a); Not(n,b); And(n,R.notNat(n,a),R.notNat(n,b));
    Not(n,R.andNat(n,R.notNat(n,a),R.notNat(n,b)));
    IntegerBitwiseModel.unfoldOr(n,a,b);
    reveal R.orNat();
  }
  lemma {:induction false} Xor(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.xorNat(n,a,b) == R.xorNat(n,a,b)
  {
    Or(n,a,b); And(n,a,b); Not(n,R.andNat(n,a,b));
    And(n,R.orNat(n,a,b),R.notNat(n,R.andNat(n,a,b)));
    IntegerBitwiseModel.unfoldXor(n,a,b);
    reveal R.xorNat();
  }
  lemma {:induction false} AndNot(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.andNotNat(n,a,b) == R.andNotNat(n,a,b)
  {
    Not(n,b); And(n,a,R.notNat(n,b));
    IntegerBitwiseModel.unfoldAndNot(n,a,b);
    reveal R.andNotNat();
  }
  lemma {:induction false} LeftShift(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.shlNat(n,a,k) == R.shlNat(n,a,k)
  {
    Power(n);
    if 0 <= k < n { Normalize(n,a); Power(k); }
    IntegerBitwiseModel.unfoldShl(n,a,k);
    reveal R.shlNat();
  }
  lemma {:induction false} RightShift(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.shrNat(n,a,k) == R.shrNat(n,a,k)
  {
    if 0 <= k < n { Normalize(n,a); Power(k); }
    IntegerBitwiseModel.unfoldShr(n,a,k);
    reveal R.shrNat();
  }
  lemma {:induction false} LeftRotate(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.rolNat(n,a,k) == R.rolNat(n,a,k)
  {
    if n > 0 { Normalize(n,a); Power(n); Power(k%n); Power(n-k%n); }
    IntegerBitwiseModel.unfoldRol(n,a,k);
    reveal R.rolNat();
  }
  lemma {:induction false} RightRotate(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.rorNat(n,a,k) == R.rorNat(n,a,k)
  {
    if n > 0 { Normalize(n,a); Power(k%n); Power(n-k%n); }
    IntegerBitwiseModel.unfoldRor(n,a,k);
    reveal R.rorNat();
  }
  lemma {:induction false} Population(n: nat, a: int)
    ensures IntegerBitwiseModel.popNat(n,a) == R.popNat(n,a)
    decreases n
  {
    IntegerBitwiseModel.unfoldPop(n,a);
    reveal R.popNat();
    if n > 0 { Population(n-1,a/2); }
  }
}
