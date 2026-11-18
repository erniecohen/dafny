// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --target py "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of classes, compiled.  The types
// 'C', 'C?', 'raw C' and 'raw C?' are the class in the target.  A test from 'object?' to 'C' or 'C?'
// tests the dynamic type alone, since every object of the class satisfies its invariants; a test of
// 'raw C?' against 'raw C' tests for null.  A raw reference, once proved finished, is passed as is.

class Cell {
  const k: int
  invariant k > 0
  constructor (k: int) requires k > 0 { this.k := k; }
  function Area(): int { k }
  function Inverse(): int { 100 / k }
}

class Plain {
  constructor () { }
  function Area(): int { 0 }
}

method Classify(o: object?) returns (s: string) {
  if o is Cell {
    var c := o as Cell;
    s := "Cell " + IntToString(c.Inverse());
  } else if o is Plain {
    s := "Plain";
  } else if o == null {
    s := "null";
  } else {
    s := "other";
  }
}

method ClassifyNullable(o: object?) returns (s: string) {
  if o is Cell? {
    s := if o == null then "Cell? null" else "Cell? area " + IntToString((o as Cell).Area());
  } else {
    s := "not a Cell?";
  }
}

method FromRaw(r: raw Cell?) returns (s: string) {
  if r is raw Cell {
    s := "raw non-null";
  } else {
    s := "raw null";
  }
}

method Finish(r: raw Cell) returns (c: Cell)
  requires r.k > 0
{
  c := r;
}

class Holder {
  var c: Cell?
  const cells: array<Cell?>
  constructor () ensures cells.Length == 2 { cells := new Cell?[2]; }
}

function IntToString(i: int): string
  decreases if i < 0 then 1 - i else i
{
  if i < 0 then "-" + IntToString(-i)
  else if i < 10 then [['0','1','2','3','4','5','6','7','8','9'][i]]
  else IntToString(i / 10) + IntToString(i % 10)
}

method Main() {
  var c := new Cell(4);
  var p := new Plain();
  var s := Classify(c); print s, "\n";
  s := Classify(p); print s, "\n";
  s := Classify(null); print s, "\n";
  s := ClassifyNullable(c); print s, "\n";
  s := ClassifyNullable(p); print s, "\n";
  s := ClassifyNullable(null); print s, "\n";
  s := FromRaw(c); print s, "\n";
  s := FromRaw(null); print s, "\n";
  var cs: set<Cell> := {c};
  var os: set<object> := cs;
  print |os|, "\n";
  var r: raw Cell := c;
  var f := Finish(r);
  print f.Inverse(), "\n";
  var h := new Holder();
  h.c := f;
  print h.c != null && h.cells[0] == null, "\n";
}
