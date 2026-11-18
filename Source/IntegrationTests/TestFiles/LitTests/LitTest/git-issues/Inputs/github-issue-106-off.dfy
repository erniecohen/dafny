// Without --refinement-renaming, every renaming is an error, as before.
abstract module A {
  function F(x: int): int
  function R(x: int): (r: int)
  function G<T>(t: T): T
  method M(a: int) returns (b: int)
  class C<U> { }
}
module B refines A {
  function F(y: int): int { y }
  function R(x: int): (s: int)
  function G<S>(t: S): S
  method M(p: int) returns (q: int)
  class C<W> ... { }
}
