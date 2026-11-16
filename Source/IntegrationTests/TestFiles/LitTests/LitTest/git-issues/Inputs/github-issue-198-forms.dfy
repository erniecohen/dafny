// The issue's program, with depth-three terms, a predicate whose conjuncts come in the
// other order, a three-way conjunction, and the same calls from a method and a lemma.
datatype Op = P | M
datatype T = Var(i: nat) | Bin(op: Op, a: T, b: T) | Tri(x: T, y: T, z: T) {
  predicate scoped(n: nat) {
    match this
    case Var(i) => i < n
    case Bin(_, a, b) => a.scoped(n) && b.scoped(n)
    case Tri(x, y, z) => x.scoped(n) && y.scoped(n) && z.scoped(n)
  }
  predicate scopedRev(n: nat) {
    match this
    case Var(i) => i < n
    case Bin(_, a, b) => b.scopedRev(n) && a.scopedRev(n)
    case Tri(x, y, z) => z.scopedRev(n) && y.scopedRev(n) && x.scopedRev(n)
  }
}

function size(t: T, s: seq<int>): int requires t.scoped(|s|) { |s| }
function sizeRev(t: T, s: seq<int>): int requires t.scopedRev(|s|) { |s| }

function Literal(x: int, y: int): int { size(Bin(P, Var(0), Var(1)), [x, y]) }
function Variable(op: Op, x: int, y: int): int { size(Bin(op, Var(0), Var(1)), [x, y]) }
function Literal3(x: int, y: int, z: int): int { size(Bin(P, Var(0), Bin(M, Var(1), Var(2))), [x, y, z]) }
function Variable3(op: Op, x: int, y: int, z: int): int { size(Bin(op, Var(2), Bin(op, Var(1), Var(0))), [x, y, z]) }
function VariableTri(u: T, x: int, y: int, z: int): int requires u.scoped(3) { size(Tri(Var(2), u, Var(0)), [x, y, z]) }
function LiteralRev(x: int, y: int): int { sizeRev(Bin(P, Var(0), Var(1)), [x, y]) }
function VariableRev(op: Op, x: int, y: int): int { sizeRev(Bin(op, Var(0), Var(1)), [x, y]) }
function VariableRev3(op: Op, x: int, y: int, z: int): int { sizeRev(Bin(op, Var(2), Bin(op, Var(1), Var(0))), [x, y, z]) }

method MVariable(op: Op, x: int, y: int) returns (r: int) { r := size(Bin(op, Var(0), Var(1)), [x, y]); }
lemma LVariable3(op: Op, x: int, y: int, z: int) ensures Bin(op, Var(2), Bin(op, Var(1), Var(0))).scoped(|[x, y, z]|) {}
