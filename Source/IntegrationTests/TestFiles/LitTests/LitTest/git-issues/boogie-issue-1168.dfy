// RUN: %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// boogie-org/boogie#1168: Boogie's arguments type encoding, which Dafny uses, had reverse
// casts such as forall x: U :: int_2_U(U_2_int(x)) == x, which say that every value is an
// int (a bool, ...) and have no model beside the casts' left inverses. Dafny now builds
// against a Boogie that removes those reverse casts. The regression checks preservation
// of typed reads. Proofs had relied on those casts
// to join a built-in value read from a collection, field or boxed position with its cast.
// The fix preserves those proofs by passing such a value to the solver as a cast. Each
// method below needs that: each verifies under 4.11.0 and with the fix, and fails when the
// reverse casts are dropped without it.

type Six = x | 6 <= x witness 7

method ArrayOfSubsetType()
{
  var a := new Six[12];
  assert 6 <= a[6];
}

method SeqOfSubsetType(s: seq<Six>, i: int)
  requires 0 <= i < |s|
{
  assert 6 <= s[i];
}

method MapOfSubsetType(m: map<int, Six>, k: int)
  requires k in m
{
  assert 6 <= m[k];
}

class C {
  var f: Six
}

method FieldOfSubsetType(c: C)
{
  assert 6 <= c.f;
}

method SuchThatInASet(s: set<int>)
  requires s != {}
{
  var z :| z in s;
  if s != {z} {
    var s' := s - {z};
    assert forall y :: y in s ==> y in s' || y == z;
  }
}
