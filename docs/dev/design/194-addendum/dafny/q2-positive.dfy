// Proposed extension; outcomes UNVERIFIED. Requires --type-system-refresh
// --user-defined-operators and the expression-surface milestone.
// All good() assertions should verify. false_control() must fail at assert false.
datatype V = V(xs: seq<int>) {
  function {:operator "[:=]"} put(i: int, v: int): V
    requires 0 <= i < |xs|
  { V(xs[i := v]) }

  function {:operator "[_.._]"} between(lo: int, hi: int): V
    requires 0 <= lo <= hi <= |xs|
  { V(xs[lo..hi]) }

  function {:operator "[_..]"} from(lo: int): V
    requires 0 <= lo <= |xs|
  { V(xs[lo..]) }

  function {:operator "[.._]"} until(hi: int): V
    requires 0 <= hi <= |xs|
  { V(xs[..hi]) }

  function {:operator "[..]"} all(): V { this }
}

datatype Grid = Grid(cells: map<(int, int), int>) {
  function {:operator "[:=]"} put(k: (int, int), v: int): Grid
  { Grid(cells[k := v]) }
}

method good() {
  var x := V([10, 20, 30]);
  var alias := x;
  var y := x[0 := 7][2 := 9];
  assert y.xs == [7, 20, 9];
  assert alias == x && x.xs == [10, 20, 30];
  assert x[0 := 7][0 := 8].xs == [8, 20, 30];
  assert x[1..3].xs == [20, 30];
  assert x[1..].xs == [20, 30];
  assert x[..2].xs == [10, 20];
  assert x[..] == x;
  assert x[1..][0 := 99].xs == [99, 30];
  var g := Grid(map[])[(1, 2) := 42][(2, 1) := 13];
  assert g.cells[(1, 2)] == 42 && g.cells[(2, 1)] == 13;
}

lemma false_control() {
  var x := V([10, 20]);
  var y := x[0 := 7][..1];
  assert y.xs == [7];
  assert false; // Expected verifier failure: reachable, nonvacuous context.
}
