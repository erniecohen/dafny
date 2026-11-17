// Run by ../github-issue-374.dfy (https://github.com/erniecohen/dafny/issues/374).
// Negative controls for the {:functionDomain} check: each declaration is rejected
// with function domains on.

module Malformed {
  // An unknown kind.
  ghost function {:axiom} {:functionDomain "domain"} R0<A, B>(f: A -> B): A -> B
  // A body.
  ghost function {:functionDomain "restrict"} R1<A, B>(f: A -> B): A -> B { f }
  // A specification.
  ghost function {:axiom} {:functionDomain "restrict"} R2<A, B>(f: A -> B): A -> B
    ensures R2(f) == f
  // A partial arrow.
  ghost function {:axiom} {:functionDomain "restrict"} R3<A, B>(f: A --> B): A --> B
  // One type parameter.
  ghost predicate {:axiom} {:functionDomain "exactDom"} E0<A>(f: A -> int)
  // The wrong result type.
  ghost function {:axiom} {:functionDomain "exactDom"} E1<A, B>(f: A -> B): int
}
