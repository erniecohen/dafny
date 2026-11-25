module Bad {
  ghost predicate {:functionDomain "pointwise"} Body<A, R>(f: A -> R, g: A -> R) { false }
  ghost predicate {:functionDomain "pointwise"} Older<A, R>(older f: A -> R, g: A -> R)
  ghost predicate {:functionDomain "pointwise"} One<A, R>(f: A -> R)
  ghost predicate {:functionDomain "pointwise"} Partial<A, R>(f: A --> R, g: A --> R)
  ghost predicate {:functionDomain "pointwise"} Reading<A, R>(f: A ~> R, g: A ~> R)
  ghost predicate {:functionDomain "pointwise"} Swapped<A, B, R>(f: (B, A) -> R, g: (B, A) -> R)
  ghost predicate {:functionDomain "pointwise"} Specification<A, R>(f: A -> R, g: A -> R)
    requires f == g
  ghost function {:functionDomain "pointwise"} Result<A, R>(f: A -> R, g: A -> R): int
  predicate {:functionDomain "pointwise"} Compiled<A, R>(f: A -> R, g: A -> R)
  ghost opaque predicate {:functionDomain "pointwise"} Opaque<A, R>(f: A -> R, g: A -> R)
}
class Member {
  ghost predicate {:functionDomain "pointwise"} Instance<A, R>(f: A -> R, g: A -> R)
}
type EmptyBool = b: bool | false witness *
ghost function {:functionDomain "pointwise"} SubsetResult<A, R>(f: A -> R, g: A -> R): EmptyBool
