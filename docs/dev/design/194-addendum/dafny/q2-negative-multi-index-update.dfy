// UNVERIFIED expected parser rejection in the pinned and proposed grammars.
// Existing multi-index READ syntax does not imply multi-index UPDATE syntax.
// Use x[(i, j) := v] with one tuple-key updater, or a named multi-index call.
datatype Grid = Grid(cells: map<(int, int), int>) {
  function put(i: int, j: int, v: int): Grid
  { Grid(cells[(i, j) := v]) }
}
method bad() {
  var x := Grid(map[]);
  var y := x[1, 2 := 42]; // Expected parser error at :=.
}
lemma false_control() {
  assert false; // Not reached after parser rejection; use q2-negative.dfy control.
}
