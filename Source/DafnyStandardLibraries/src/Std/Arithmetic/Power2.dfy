/*******************************************************************************
 *  Original: Copyright (c) Microsoft Corporation
 *  SPDX-License-Identifier: MIT
 *  
 *  Modifications and Extensions: Copyright by the contributors to the Dafny Project
 *  SPDX-License-Identifier: MIT 
 *******************************************************************************/

/* Every lemma comes in 2 forms: 'LemmaProperty' and 'LemmaPropertyAuto'. The
former takes arguments and may be more stable and less reliant on Z3
heuristics. The latter includes automation and its use requires less effort */

module {:disableNonlinearArithmetic} Std.Arithmetic.Power2 {
  import opened GeneralInternals
  import opened MulInternals
  import opened Power

  function Pow2(e: nat): nat
    ensures Pow2(e) > 0
  {
    LemmaPowPositive(2, e);
    Pow(2, e)
  }

  /* Pow2() is equivalent to Pow() with base 2. */
  lemma LemmaPow2(e: nat)
    ensures Pow2(e) == Pow(2, e)
  {

    if e != 0 {
      LemmaPow2(e - 1);
    }
  }

  lemma LemmaPow2Auto()
    ensures forall e: nat {:trigger Pow2(e)} :: Pow2(e) == Pow(2, e)
  {

    forall e: nat {:trigger Pow2(e)}
      ensures Pow2(e) == Pow(2, e)
    {
      LemmaPow2(e);
    }
  }

  /* (2^e - 1) / 2 = 2^(e - 1) - 1 */
  // keep
  lemma LemmaPow2MaskDiv2(e: nat)
    requires 0 < e
    ensures (Pow2(e) - 1) / 2 == Pow2(e - 1) - 1
  {
    LemmaPow2Auto();
    LemmaPowAuto();
    var f := e => 0 < e ==> (Pow2(e) - 1) / 2 == Pow2(e - 1) - 1;
    assert forall i {:trigger IsLe(0, i)} :: IsLe(0, i) && f(i) ==> f(i + 1);
    assert forall i {:trigger IsLe(i, 0)} :: IsLe(i, 0) && f(i) ==> f(i - 1);
    LemmaMulInductionAuto(e, f);
  }

  lemma LemmaPow2MaskDiv2Auto()
    ensures forall e: nat {:trigger Pow2(e)} :: 0 < e ==>
                                                  (Pow2(e) - 1) / 2 == Pow2(e - 1) - 1
  {
    forall e: nat {:trigger Pow2(e)} | 0 < e
      ensures (Pow2(e) - 1) / 2 == Pow2(e - 1) - 1
    {
      LemmaPow2MaskDiv2(e);
    }
  }

  lemma Lemma2To64()
    ensures Pow2(0) == 0x1
    ensures Pow2(1) == 0x2
    ensures Pow2(2) == 0x4
    ensures Pow2(3) == 0x8
    ensures Pow2(4) == 0x10
    ensures Pow2(5) == 0x20
    ensures Pow2(6) == 0x40
    ensures Pow2(7) == 0x80
    ensures Pow2(8) == 0x100
    ensures Pow2(9) == 0x200
    ensures Pow2(10) == 0x400
    ensures Pow2(11) == 0x800
    ensures Pow2(12) == 0x1000
    ensures Pow2(13) == 0x2000
    ensures Pow2(14) == 0x4000
    ensures Pow2(15) == 0x8000
    ensures Pow2(16) == 0x10000
    ensures Pow2(17) == 0x20000
    ensures Pow2(18) == 0x40000
    ensures Pow2(19) == 0x80000
    ensures Pow2(20) == 0x100000
    ensures Pow2(21) == 0x200000
    ensures Pow2(22) == 0x400000
    ensures Pow2(23) == 0x800000
    ensures Pow2(24) == 0x1000000
    ensures Pow2(25) == 0x2000000
    ensures Pow2(26) == 0x4000000
    ensures Pow2(27) == 0x8000000
    ensures Pow2(28) == 0x10000000
    ensures Pow2(29) == 0x20000000
    ensures Pow2(30) == 0x40000000
    ensures Pow2(31) == 0x80000000
    ensures Pow2(32) == 0x100000000
    ensures Pow2(64) == 0x10000000000000000
  {
    // One doubling per step, so that no step unfolds Pow more than once.
    assert Pow2(0) == 0x1;
    assert Pow2(1) == 2 * Pow2(0) == 0x2;
    assert Pow2(2) == 2 * Pow2(1) == 0x4;
    assert Pow2(3) == 2 * Pow2(2) == 0x8;
    assert Pow2(4) == 2 * Pow2(3) == 0x10;
    assert Pow2(5) == 2 * Pow2(4) == 0x20;
    assert Pow2(6) == 2 * Pow2(5) == 0x40;
    assert Pow2(7) == 2 * Pow2(6) == 0x80;
    assert Pow2(8) == 2 * Pow2(7) == 0x100;
    assert Pow2(9) == 2 * Pow2(8) == 0x200;
    assert Pow2(10) == 2 * Pow2(9) == 0x400;
    assert Pow2(11) == 2 * Pow2(10) == 0x800;
    assert Pow2(12) == 2 * Pow2(11) == 0x1000;
    assert Pow2(13) == 2 * Pow2(12) == 0x2000;
    assert Pow2(14) == 2 * Pow2(13) == 0x4000;
    assert Pow2(15) == 2 * Pow2(14) == 0x8000;
    assert Pow2(16) == 2 * Pow2(15) == 0x10000;
    assert Pow2(17) == 2 * Pow2(16) == 0x20000;
    assert Pow2(18) == 2 * Pow2(17) == 0x40000;
    assert Pow2(19) == 2 * Pow2(18) == 0x80000;
    assert Pow2(20) == 2 * Pow2(19) == 0x100000;
    assert Pow2(21) == 2 * Pow2(20) == 0x200000;
    assert Pow2(22) == 2 * Pow2(21) == 0x400000;
    assert Pow2(23) == 2 * Pow2(22) == 0x800000;
    assert Pow2(24) == 2 * Pow2(23) == 0x1000000;
    assert Pow2(25) == 2 * Pow2(24) == 0x2000000;
    assert Pow2(26) == 2 * Pow2(25) == 0x4000000;
    assert Pow2(27) == 2 * Pow2(26) == 0x8000000;
    assert Pow2(28) == 2 * Pow2(27) == 0x10000000;
    assert Pow2(29) == 2 * Pow2(28) == 0x20000000;
    assert Pow2(30) == 2 * Pow2(29) == 0x40000000;
    assert Pow2(31) == 2 * Pow2(30) == 0x80000000;
    assert Pow2(32) == 2 * Pow2(31) == 0x100000000;
    assert Pow2(33) == 2 * Pow2(32) == 0x200000000;
    assert Pow2(34) == 2 * Pow2(33) == 0x400000000;
    assert Pow2(35) == 2 * Pow2(34) == 0x800000000;
    assert Pow2(36) == 2 * Pow2(35) == 0x1000000000;
    assert Pow2(37) == 2 * Pow2(36) == 0x2000000000;
    assert Pow2(38) == 2 * Pow2(37) == 0x4000000000;
    assert Pow2(39) == 2 * Pow2(38) == 0x8000000000;
    assert Pow2(40) == 2 * Pow2(39) == 0x10000000000;
    assert Pow2(41) == 2 * Pow2(40) == 0x20000000000;
    assert Pow2(42) == 2 * Pow2(41) == 0x40000000000;
    assert Pow2(43) == 2 * Pow2(42) == 0x80000000000;
    assert Pow2(44) == 2 * Pow2(43) == 0x100000000000;
    assert Pow2(45) == 2 * Pow2(44) == 0x200000000000;
    assert Pow2(46) == 2 * Pow2(45) == 0x400000000000;
    assert Pow2(47) == 2 * Pow2(46) == 0x800000000000;
    assert Pow2(48) == 2 * Pow2(47) == 0x1000000000000;
    assert Pow2(49) == 2 * Pow2(48) == 0x2000000000000;
    assert Pow2(50) == 2 * Pow2(49) == 0x4000000000000;
    assert Pow2(51) == 2 * Pow2(50) == 0x8000000000000;
    assert Pow2(52) == 2 * Pow2(51) == 0x10000000000000;
    assert Pow2(53) == 2 * Pow2(52) == 0x20000000000000;
    assert Pow2(54) == 2 * Pow2(53) == 0x40000000000000;
    assert Pow2(55) == 2 * Pow2(54) == 0x80000000000000;
    assert Pow2(56) == 2 * Pow2(55) == 0x100000000000000;
    assert Pow2(57) == 2 * Pow2(56) == 0x200000000000000;
    assert Pow2(58) == 2 * Pow2(57) == 0x400000000000000;
    assert Pow2(59) == 2 * Pow2(58) == 0x800000000000000;
    assert Pow2(60) == 2 * Pow2(59) == 0x1000000000000000;
    assert Pow2(61) == 2 * Pow2(60) == 0x2000000000000000;
    assert Pow2(62) == 2 * Pow2(61) == 0x4000000000000000;
    assert Pow2(63) == 2 * Pow2(62) == 0x8000000000000000;
    assert Pow2(64) == 2 * Pow2(63) == 0x10000000000000000;
  }

}
