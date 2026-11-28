// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
include "IntegerBitwiseCompilerFoundation.dfy"

// Explicit checked facts for PopCount source clients; no assumed range facts.
module Std.Arithmetic.IntegerBitwisePopulation {
  import F = IntegerBitwiseCompilerFoundation
  import R = IntegerBitwise

  lemma View(n:nat, a:int)
    ensures IntegerBitwiseModel.popNat(n,a) == R.popNat(n,a)
    ensures IntegerBitwiseModel.popNat(n,a) == R.count(R.bitsNat(n,a))
    ensures 0 <= IntegerBitwiseModel.popNat(n,a) <= n
  { F.Population(n,a); R.PopulationView(n,a); }

  lemma {:induction false} AllOnes(n:nat)
    ensures R.popNat(n,R.P(n)-1) == n
    ensures IntegerBitwiseModel.popNat(n,R.P(n)-1) == n
    decreases n
  {
    reveal R.popNat(), R.P();
    if n > 0 { AllOnes(n-1); }
    F.Population(n,R.P(n)-1);
  }
}
