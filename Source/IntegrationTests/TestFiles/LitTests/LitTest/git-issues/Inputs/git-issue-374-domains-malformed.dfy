// Each malformed annotation is rejected before its semantic rule is emitted.
module Bad {
  ghost predicate {:functionDomain "equal"} FalseBody<A, B, R>(f: (A, B) -> R, g: (A, B) -> R) { false }
  ghost predicate {:functionDomain "equal"} Reverse<A, B, R>(f: (A, B) -> R, g: (A, B) -> R) { f != g }
  ghost predicate {:functionDomain "equal"} WrongParameters<A, B, R>(f: (A, B) -> R, g: (A, B) -> R) { f == f }
  ghost predicate {:functionDomain "equal"} NoBody<A, B, R>(f: (A, B) -> R, g: (A, B) -> R)
  ghost predicate {:functionDomain "equal"} Partial<A, B, R>(f: (A, B) --> R, g: (A, B) --> R) { f == g }
  ghost predicate {:functionDomain "equal"} Reading<A, B, R>(f: (A, B) ~> R, g: (A, B) ~> R) { f == g }
  ghost predicate {:functionDomain "equal"} Swapped<A, B, R>(f: (B, A) -> R, g: (B, A) -> R) { f == g }
  ghost predicate {:functionDomain "equal"} One<A, B, R>(f: (A, B) -> R) { f == f }
  ghost predicate {:functionDomain "equal"} Spec<A, B, R>(f: (A, B) -> R, g: (A, B) -> R)
    requires f == g
  { f == g }
  ghost function {:functionDomain "restrict"} EtaWrong<A, R>(f: A -> R): A -> R { f }
  ghost function {:functionDomain "restrict"} EtaApplication<A, R>(f: A -> R): A -> R { (x: A) => f(x) }
  ghost function {:functionDomain "restrict"} EtaPermutation<A, R>(f: (A, A) -> R): (A, A) -> R { (x: A, y: A) => f(y, x) }
  ghost function {:functionDomain "restrict"} EtaType<A, R>(f: int -> R): nat -> R { (x: nat) => f(x) }
  ghost function {:functionDomain "restrict"} EtaRange<A, R>(f: A -> R): A -> R { (x: A) requires true => f(x) }
  ghost function {:functionDomain "restrict"} EtaReads<A(!new), R>(f: A -> R): A -> R { (x: A) reads f.reads => f(x) }
  ghost predicate {:functionDomain "exactDom"} DomBody<A, R>(f: A -> R) { true }
  function {:functionDomain "restrict"} Compiled<A, R>(f: A -> R): A -> R { (x: A) => f(x) }
  ghost opaque predicate {:functionDomain "equal"} Opaque<A, R>(f: A -> R, g: A -> R) { f == g }
}
class Member {
  ghost predicate {:functionDomain "equal"} Instance<A, R>(f: A -> R, g: A -> R) { f == g }
}
