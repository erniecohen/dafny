// NONUNIFORM: the bound results of a lemma are ghost and are erased before any backend sees them
// RUN: %run --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: a compiled expression may bind the results of a lemma and
// use them in ghost positions.  The binding is erased, as a lemma call in an expression is.

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

function Next(n: nat): (r: nat)
  ensures r > n
{
  ghost var m := Find(n);
  var k := n + 1;
  assert m > n;
  k
}

function Unused(n: nat): nat {
  var m := Find(n);
  n + 2
}

datatype D = D(x: nat, ghost y: nat)

function GhostField(n: nat): D {
  D(n, var m := Find(n); m)
}

class Cell {
  var f: nat
  constructor (f: nat)
    ensures this.f == f
  {
    this.f := f;
  }
}

lemma AtLeast(c: Cell) returns (m: nat)
  ensures m >= c.f
{
  m := c.f;
}

function ReadsTheHeap(c: Cell): nat
  reads c
{
  var m := AtLeast(c);
  assert m >= c.f;
  c.f
}

method Main() {
  var c := new Cell(7);
  ghost var g := var m := Find(1); m;
  assert g > 1;
  var s := seq(3, (i: nat) => var m: nat := Find(i); i * 2);
  print Next(3), " ", Unused(4), " ", GhostField(5).x, " ", ReadsTheHeap(c), " ", s, "\n";
}
