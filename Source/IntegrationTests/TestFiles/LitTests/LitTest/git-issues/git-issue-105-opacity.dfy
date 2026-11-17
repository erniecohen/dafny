// Member invariants (https://github.com/erniecohen/dafny/issues/105) are never opaque.  Under
// --default-function-opacity, every other function gets a reveal lemma, but an invariant gets none,
// so the invariants of traits that meet in a diamond, or that a trait inherits and adds to, do not
// clash, and each invariant's well-formedness, which assumes the invariants before it, verifies.
// RUN: %verify --member-invariants --default-function-opacity opaque "%s" > "%t"
// RUN: %verify --member-invariants --default-function-opacity autoRevealDependencies "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

trait Top {
  const k: int
  invariant k >= 0
  function Twice(): int { 2 * k }
}
trait Left extends Top {
  invariant k < 100
}
trait Right extends Top {
  invariant k < 200
}
class Both extends Left, Right {
  invariant k != 7
  constructor () { k := 0; }
  function Next(): int { Twice() + 1 }
}
datatype E = E(n: nat) {
  invariant n < 5
  function G(): nat { n }
}
