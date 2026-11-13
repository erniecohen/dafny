// Proposed [] syntax; expected outcomes are UNVERIFIED (extension not implemented).
// --user-defined-operators --type-system-refresh
// All declarations except q1_false_control should verify; that lemma must fail
// exactly at its assert false. Main should print 20,4,42 on separate lines.

datatype SeqBox = SeqBox(xs: seq<int>) {
  function {:operator "[]"} get(i: int): int
    requires 0 <= i < |xs|
  { xs[i] }
}

datatype Grid = Grid(a: int, b: int, c: int, d: int) {
  static function {:operator "[]"} at(g: Grid, i: int, j: int): int
    requires 0 <= i < 2 && 0 <= j < 2
  {
    if i == 0 then (if j == 0 then g.a else g.b)
    else (if j == 0 then g.c else g.d)
  }
}

datatype Catalog = Catalog(answer: int) {
  function {:operator "[]"} get(key: string): int
    requires key == "answer"
  { answer }
}

datatype GhostBox = GhostBox(n: int) {
  ghost function {:operator "[]"} get(i: int): int { n + i }
}

class HeapBox {
  var a: array<int>

  constructor(a': array<int>) { a := a'; }

  function {:operator "[]"} get(i: int): int
    requires 0 <= i < a.Length
    reads this, a
  { a[i] } by method { return a[i]; }
}

function heap_read(h: HeapBox, i: int): int
  requires 0 <= i < h.a.Length
  reads h, h.a
{ h[i] }

lemma read_examples()
{
  var b := SeqBox([10, 20]);
  var g := Grid(1, 2, 3, 4);
  var c := Catalog(42);
  assert b[1] == b.get(1) == 20;
  assert g[1, 1] == Grid.at(g, 1, 1) == 4;
  assert c["answer"] == c.get("answer") == 42;
  assert GhostBox(4)[2] == 6;
}

method Main()
{
  var b := SeqBox([10, 20]);
  var g := Grid(1, 2, 3, 4);
  var c := Catalog(42);
  print b[1], "\n", g[1, 1], "\n", c["answer"], "\n";
}

lemma q1_false_control()
{
  var b := SeqBox([10, 20]);
  assert b[0] == 10;
  assert false; // Must fail: operator syntax introduces no new fact.
}
