// Positive controls for https://github.com/erniecohen/dafny/issues/495: conversions to a newtype
// whose base type is inferred that satisfy every constraint still verify, and the base's
// constraint is still available as a fact about every value of the newtype.

newtype NewSix = x | 6 <= x witness 6
predicate Small(x: NewSix) { x < 100 }
newtype Y0 = six | Small(six) witness 6    // base inferred as NewSix
newtype Y4 = w: Y0 | w as int < 50 witness 6

method Literal() returns (y: Y0) ensures y as int == 9 {
  y := 9;
}

method FromInt(i: int) returns (y: Y0) requires 6 <= i < 100 ensures y as int == i {
  y := i as Y0;
}

method FromReal(r: real) returns (y: Y0) requires r == 7.0 {
  y := r as Y0;
}

method FromBitvector(b: bv8) returns (y: Y0) requires 6 <= b as int < 100 {
  y := b as Y0;
}

method FromBase(n: NewSix) returns (y: Y0) requires n < 100 {
  y := n as Y0;
}

function FromIntFunction(i: int): Y0 requires 6 <= i < 100 {
  i as Y0
}

method TwoDeep(i: int) returns (w: Y4) requires 6 <= i < 50 {
  w := i as Y4;
}

method BaseFact(y: Y0, w: Y4) {
  assert 6 <= y as int && Small(y as NewSix);
  assert 6 <= w as int < 50;
}
