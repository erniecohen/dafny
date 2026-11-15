// Controls: an index out of scope still fails the precondition, and with the
// precondition proved, "assert false" still fails.
datatype Op = P | M
datatype T = Var(i: nat) | Bin(op: Op, a: T, b: T) {
  predicate scoped(n: nat) {
    match this
    case Var(i) => i < n
    case Bin(_, a, b) => a.scoped(n) && b.scoped(n)
  }
}

function size(t: T, s: seq<int>): int requires t.scoped(|s|) { |s| }

function OutOfScope(op: Op, x: int, y: int): int { size(Bin(op, Var(0), Var(2)), [x, y]) }

lemma False(op: Op, x: int, y: int) {
  var r := size(Bin(op, Var(0), Var(1)), [x, y]);
  assert false;
}
