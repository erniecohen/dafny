// Formation and characteristic errors: the order must be nat or ORDINAL, the payload an arrow type, and a bounded type
// has no compiled equality, no default value and no nonemptiness.
module N {
  type Small = x: nat | x < 10
  type F = int -> int
  type N = nat
  method BadOrder(a: bounded<int, int -> int>, b: bounded<Small, F>, c: bounded<real, F>, ok: bounded<N, F>)
  method Generic<O, G>(a: bounded<O, int -> int>, b: bounded<nat, G>)
  method BadPayload(a: bounded<nat, int>, b: bounded<nat, seq<F>>)
  method Twice(a: bounded<int, int -> int>) { var b := a; var c := [a, b]; }
  method Equality(c: bounded<nat, int -> int>, d: bounded<nat, int -> int>) returns (r: int) {
    if c == d { r := 0; }
    ghost var g := c == d;
  }
  function Auto<T(0)>(): T
  function Nonempty<T(00)>(): int
  ghost function UseAuto(): bounded<nat, int -> int> { Auto() }
  ghost function UseNonempty(): int { Nonempty<bounded<nat, int -> int>>() }
  method Set(c: bounded<nat, int -> int>) { var s := {c}; ghost var t := {c}; var q := [c]; }
}
