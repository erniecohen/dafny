module Bad {
  ghost predicate {:functionDomain "partialEqual"} WrongBody<A, R>(f: A --> R, g: A --> R) { false }
  ghost predicate {:functionDomain "partialEqual"} WrongOrder<A, B, R>(f: (B, A) --> R, g: (B, A) --> R) { f == g }
  ghost predicate {:functionDomain "partialEqual"} Reading<A, R>(f: A ~> R, g: A ~> R) { f == g }
  ghost predicate {:functionDomain "partialEqual"} Total<A, R>(f: A -> R, g: A -> R) { f == g }
  ghost predicate {:functionDomain "partialEqual"} Older<A, R>(older f: A --> R, g: A --> R) { f == g }
  ghost predicate {:functionDomain "partialEqual"} Specification<A, R>(f: A --> R, g: A --> R) requires f == g { f == g }
  ghost predicate {:functionDomain "partialEqual"} NoBody<A, R>(f: A --> R, g: A --> R)
  ghost predicate {:functionDomain "partialPointwise"} Body<A, R>(f: A --> R, g: A --> R) { true }
  ghost predicate {:functionDomain "partialPointwise"} ReadingP<A, R>(f: A ~> R, g: A ~> R)
  ghost predicate {:functionDomain "partialPointwise"} OlderP<A, R>(older f: A --> R, g: A --> R)
  ghost predicate {:functionDomain "partialExactDom"} BodyD<A, R>(f: A --> R) { true }
  ghost predicate {:functionDomain "partialExactDom"} ReadingD<A, R>(f: A ~> R)
  ghost opaque predicate {:functionDomain "partialEqual"} Opaque<A, R>(f: A --> R, g: A --> R) { f == g }
}
type EmptyBool = b: bool | false witness *
ghost function {:functionDomain "partialPointwise"} Result<A, R>(f: A --> R, g: A --> R): EmptyBool
class Member {
  ghost predicate {:functionDomain "partialEqual"} Instance<A, R>(f: A --> R, g: A --> R) { f == g }
}
