include "../../../../../../DafnyStandardLibraries/optional/IntegerBitwise.dfy"

module Issue437 {
  import M = Std.Arithmetic.Mul
  import MI = Std.Arithmetic.MulInternals
  import D = Std.Arithmetic.DivMod
  import B = Std.Arithmetic.IntegerBitwise
  import S = Std.Collections.Seq
  import R = Std.Relations

  lemma PositiveMul()
  {
    M.LemmaMulIsMulRecursive(-2, 3);
    assert MI.MulRecursive(-2, 3) == -6;
  }

  lemma PositiveDiv()
  {
    D.LemmaMultiplyDivideLt(-7, 3, -2);
    assert -7 / 3 < -2;
  }

  lemma PositiveQuotient()
  {
    B.Quotient(-7, 3, -3, 2);
    assert -7 / 3 == -3 && -7 % 3 == 2;
  }

  lemma PositiveMerge()
  {
    var le := (x: int, y: int) => x <= y;
    assert R.TotalOrdering(le);
    var merged := S.MergeSortedWith([1, 3], [2, 4], le);
    assert R.SortedBy(le, merged);
    assert multiset(merged) == multiset([1, 2, 3, 4]);
  }

  lemma NegativeMul() {
    M.LemmaMulIsMulRecursive(-2, 3);
    assert false;
  }

  lemma NegativeDiv() {
    D.LemmaMultiplyDivideLt(-7, 3, -2);
    assert false;
  }

  lemma NegativeQuotient() {
    B.Quotient(-7, 3, -3, 2);
    assert false;
  }

  lemma NegativeMerge() {
    hide S.MergeSortedWith;
    var le := (x: int, y: int) => x <= y;
    assert R.TotalOrdering(le);
    var merged := S.MergeSortedWith([1, 3], [2, 4], le);
    assert false;
  }
}
