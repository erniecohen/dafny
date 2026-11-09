datatype Op = P | M
datatype T = Var(i: nat) | Bin(op: Op, a: T, b: T) {
  predicate scoped(n: nat) {
    match this
    case Var(i) => i < n
    case Bin(_,a,b) => a.scoped(n) && b.scoped(n)
  }
}

function size(t: T, s: seq<int>): int requires t.scoped(|s|) { |s| }

function Literal(x: int, y: int): int { size(Bin(P,Var(0),Var(1)),[x,y]) }

function Variable(op: Op, x: int, y: int): int { size(Bin(op,Var(0),Var(1)),[x,y]) }
