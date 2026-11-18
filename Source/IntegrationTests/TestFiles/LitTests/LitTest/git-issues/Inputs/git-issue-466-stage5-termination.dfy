// The new body closes a cycle through a sibling and an inherited call. The inherited
// call must get the termination check that the base never made.
abstract module Base {
  function F(n: nat): nat decreases n, 2
  function G(n: nat): nat decreases n, 0 { F(n) }
}
module Left refines Base {
  import Right
  function F(n: nat): nat { Right.H(n) + 1 }
  lemma WouldBeBad() ensures false {
    assert F(0) == Right.H(0) + 1;
    assert Right.H(0) == G(0);
    assert G(0) == F(0);
  }
}
module Right {
  import Left
  function H(n: nat): nat decreases n, 1 { Left.G(n) }
}
