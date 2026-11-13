// PROPOSED M6 SYNTAX; EXPECTED OUTCOMES UNVERIFIED.
// --type-system-refresh --user-defined-operators
// All substantive declarations should verify. false_control must fail once.
// Each q4 file is standalone; do not concatenate these files into one program.

datatype Row = Row(s: seq<int>) {
  function {:operator "[]"} at(i: int): int
    requires 0 <= i < |s|
  { s[i] }

  function {:operator "[:=]"} put(i: int, v: int): Row
    requires 0 <= i < |s|
  { Row(s[i := v]) }
}

// This owner intentionally has no read-index slot. Assignment needs only put2.
datatype Grid = Grid(a: int, b: int, c: int, d: int) {
  function {:operator "[:=]"} put2(i: int, j: int, v: int): Grid
    requires 0 <= i < 2 && 0 <= j < 2
  {
    if i == 0 then
      if j == 0 then Grid(v,b,c,d) else Grid(a,v,c,d)
    else
      if j == 0 then Grid(a,b,v,d) else Grid(a,b,c,v)
  }
}

class Box {
  var row: Row
  constructor(r: Row)
    ensures row == r
  { row := r; }
}

// No getter is required for a class setter either.
class Buffer {
  var s: seq<int>
  constructor(s0: seq<int>)
    ensures s == s0
  { s := s0; }

  method {:operator "[]:="} put(i: int, v: int)
    requires 0 <= i < |s|
    modifies this
    ensures s == old(s)[i := v]
  { s := s[i := v]; }

  method {:operator "[]:="} put2(i: int, j: int, v: int)
    requires 0 <= i + j < |s|
    modifies this
    ensures s == old(s)[i + j := v]
  { s := s[i + j := v]; }
}

class Cell {
  var n: int
  constructor(v: int)
    ensures n == v
  { n := v; }

  static method {:operator "[]:="} put(x: Cell, i: int, v: int)
    requires i == 0
    modifies x
    ensures x.n == v
  { x.n := v; }
}

// An ordinary object-valued getter can be used as an ordinary frame expression.
datatype RefBox = RefBox(c: Cell) {
  function {:operator "[]"} at(i: int): Cell
    requires i == 0
  { c }
}

method set_returned_object(x: RefBox)
  modifies x[0]
  ensures x.c.n == 12
{ x[0].n := 12; }

function read_returned_object(x: RefBox): int
  reads x[0]
{ x[0].n }

method positive() {
  var x := Row([1,2]);
  var y := Row([8,9]);
  x[0], y[1] := y[1], x[0];
  assert x == Row([9,2]) && y == Row([8,1]);

  x[0], x[0] := 7, 7; // Aliased root is legal: equal complete replacements.
  assert x == Row([7,2]);

  var h := new Box(Row([3,4]));
  h.row[1] := 10;
  assert h.row == Row([3,10]);

  var a := new Row[1](k => Row([5,6]));
  a[0][0] := 11; // Root is an intrinsic array element holding a Row.
  assert a[0] == Row([11,6]);

  var g := Grid(1,2,3,4);
  g[1,0] := 15; // Two-index updater, no functional multi-index update syntax.
  assert g == Grid(1,2,15,4);

  var b := new Buffer([1,2,3]);
  b[0] := 8;
  b[0,1] := 9;
  assert b.s == [8,9,3];

  var c := new Cell(1);
  c[0] := 6; // Static setter, first explicit formal is the owner.
  assert c.n == 6;
}

lemma false_control() {
  var x := Row([0]);
  x[0] := 1;
  assert x == Row([1]);
  assert false; // EXPECTED verifier failure, not a resolution failure.
}
