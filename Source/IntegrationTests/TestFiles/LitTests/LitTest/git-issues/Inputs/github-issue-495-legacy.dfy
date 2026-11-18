// Under --legacy-inferred-newtype-base-conversions, the reproducer of
// https://github.com/erniecohen/dafny/issues/495 still proves false.

newtype NewSix = x | 6 <= x witness 6
predicate Yes(x: NewSix) { true }
newtype Y0 = six | Yes(six) witness 6      // base inferred as NewSix
method Make() returns (y: Y0) ensures y as int == 3 { y := 3 as Y0; }
method Use() { var y := Make(); assert false; }
