// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// The deleted inverse made every box a value of every type. The source counterexample
// in github-issue-23-box-bad.dfy proves false with that inverse: Opt<int> and Opt<bool>
// share the datatype sort, exposing three int boxes to bool unboxing. The guarded
// inverse must preserve legitimate field proofs instead. The heap holds a box of the
// field's type, so the inverse can reconstruct the value just stored in each field.
// The constructor postconditions below check that preservation, and Vacuity checks
// that the facts in play still cannot prove false.
// The source counterexample and the preservation checks exercise distinct obligations.

class Node {
  var next: Node?
  constructor ()
  {
    next := null;
  }
}

class Queue {
  var head: Node
  ghost var spine: set<Node>

  constructor ()
    ensures head in spine
  {
    var n := new Node();
    head := n;
    spine := {n};
  }
}

class Booleans {
  var b: bool
  ghost var bs: set<bool>

  constructor (c: bool)
    ensures b in bs
  {
    b := c;
    bs := {c};
  }
}

// A vacuity control.  The facts the fix states are in play here: fields of several types are
// read and written, and their values go into collections as boxes.  With them present, false
// must still not be provable.
class Cell {
  var i: int
  var b: bool
  var next: Cell?
}

method Vacuity(c: Cell)
  modifies c
{
  c.i := c.i + 1;
  c.b := !c.b;
  var s: seq<int> := [c.i];
  var t: set<bool> := {c.b};
  var u: set<Cell?> := {c.next};
  assert s[0] == c.i && c.b in t && c.next in u;
  assert false; // error: must not be provable
}
