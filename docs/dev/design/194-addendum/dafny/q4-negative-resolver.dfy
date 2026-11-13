// PROPOSED M6 SYNTAX; EXPECTED OUTCOMES UNVERIFIED.
// --type-system-refresh --user-defined-operators
// Each marked case should fail resolution for the stated reason.
// Resolver failure prevents verification of false_control in this file.
// q4-control.dfy supplies an independently verifiable named-call control.

datatype Row = Row(s: seq<int>) {
  function {:operator "[]"} at(i: int): int
    requires 0 <= i < |s|
  { s[i] }
  function {:operator "[:=]"} put(i: int, v: int): Row
    requires 0 <= i < |s|
  { Row(s[i := v]) }
}

class Cell {
  var n: int
  method {:operator "[]:="} put(i: int, v: int)
    requires i == 0
    modifies this
  { n := v; }
}

method setter_multi(c: Cell, d: Cell)
  modifies c, d
{
  c[0], d[0] := 1, 2; // ERROR: user method setter in a multi-assignment.
}

method immutable_root(x: Row)
  requires 0 < |x.s|
{
  x[0] := 1; // ERROR: method input parameter is not an ordinary writable root.
}

method missing_arity() {
  var x := Row([1,2]);
  x[0,1] := 7; // ERROR: no two-index pure updater on Row.
}

method make_value() returns (r: int) { r := 7; }

method effectful_rhs() {
  var x := Row([1,2]);
  x[0] := make_value(); // ERROR: method RHS not in initial indexed-assignment scope.
}

method bad_frame(x: Row)
  requires 0 < |x.s|
  modifies x[0] // ERROR: getter returns int, which is not an ordinary frame type.
{ }

method aggregate_update(a: array<Row>)
  requires forall k :: 0 <= k < a.Length ==> 0 < |a[k].s|
  modifies a
{
  forall k | 0 <= k < a.Length {
    a[k][0] := 7; // ERROR: user indexed destination in aggregate forall update.
  }
  // Use ordinary whole-root assignment instead:
  // forall k | 0 <= k < a.Length { a[k] := a[k].put(0, 7); }
}

lemma false_control() {
  var x := Row([0]);
  x := x.put(0,1);
  assert x == Row([1]);
  assert false; // Must fail if verified independently; see q4-control.dfy.
}
