// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %verify "%S/Inputs/github-issue-495-positive.dfy" >> "%t"
// RUN: %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-495-positive.dfy" >> "%t"
// RUN: %verify --legacy-inferred-newtype-base-conversions "%S/Inputs/github-issue-495-legacy.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/495): a conversion to a newtype whose base
// type is inferred checked only the newtype's own constraint and skipped those of the inferred base,
// which every value of the newtype is then assumed to satisfy.  Here the base of Y0 is inferred as
// NewSix, so `3 as Y0` checked Yes(3) but never 6 <= 3, and the reproducer proved false.  The
// conversion now checks the base's constraint too, under both resolvers.  The same check covers a
// literal of the newtype and conversions from int, real and bitvector values, in methods and
// functions.  Correct conversions are accepted (Inputs/github-issue-495-positive.dfy).  Under the
// hidden --legacy-inferred-newtype-base-conversions, the reproducer still proves false
// (Inputs/github-issue-495-legacy.dfy).

newtype NewSix = x | 6 <= x witness 6
predicate Yes(x: NewSix) { true }
newtype Y0 = six | Yes(six) witness 6      // base inferred as NewSix

method Make() returns (y: Y0) ensures y as int == 3 {
  y := 3 as Y0;  // error: violates the constraint of NewSix
}

method Use() {
  var y := Make();
  assert false;  // still verifies, from Make's postcondition
}

method Literal() {
  var y: Y0 := 3;  // error
}

method FromInt(i: int) {
  var y := i as Y0;  // error
}

method FromReal(r: real) requires r.Floor as real == r {
  var y := r as Y0;  // error
}

method FromBitvector(b: bv8) {
  var y := b as Y0;  // error
}

function FromIntFunction(i: int): Y0 {
  i as Y0  // error
}

// An explicit base that is itself a newtype with an inferred base: the conversion checks the
// constraints of Y4 and, through Y0, of NewSix.
newtype Y4 = w: Y0 | w as int < 100 witness 6

method TwoDeep(i: int) {
  var w := i as Y4;  // two errors: NewSix and Y4
}
