// Many constructs in inherited code, each using a renamed name.
abstract module A {
  datatype Opt<T> = None | Some(v: T)
  datatype Res<T> = Ok(v: T) | Err {
    predicate IsFailure() { Err? }
    function PropagateFailure<U>(): Res<U> requires Err? { Err }
    function Extract(): T requires Ok? { v }
  }
  class Cell<X> { var x: X constructor (x: X) { this.x := x; } }
  function Ident<X>(x: X): X { x }
  class C<U(0)> {
    var f: U
    const c: U
    ghost var g: seq<U>
    constructor (u: U) ensures f == u { f := u; c := u; g := [u]; }
    method Locals(x: U) returns (y: U)
      modifies this
      ensures y == x
    {
      var a: U := x;
      var b: seq<U> := [a];
      ghost var q := forall z: U :: z in b ==> z == a;
      var h := (w: U) => w;
      var arr := new U[1](_ => x);
      f := h(a);
      g := g + [x];
      y := b[0];
      label L: assert old@L(f) == x;
      var o := new Cell<U>(x);
      var m: map<int, U> := map[0 := x];
      var fn: U -> U := Ident<U>;
      var arr2: array<U> := new U[] [x];
      var t: (U, seq<U>) := (x, [x]);
      var none: Opt<U> := Opt<U>.None;
      var d := Opt<U>.Some(x);
      if d is Opt<U> { }
      match d { case Some(vv: U) => case None => }
    }
    static function Pick<V>(o: Opt<V>, d: V): V {
      match o
      case None => d
      case Some(v) => v
    }
  }
  function F<T>(x: T, n: nat): (r: seq<T>)
    ensures |r| == n
    decreases n
  {
    if n == 0 then [] else
      var rest: seq<T> := F(x, n - 1);
      [x] + rest
  }
  method Stmts<T(==)>(xs: seq<T>, d: T) returns (r: T, n: nat)
    ensures n == |xs|
  {
    r, n := d, 0;
    while n < |xs|
      invariant n <= |xs|
      decreases |xs| - n
    {
      r := xs[n];
      n := n + 1;
    }
    if xs != [] {
      var s: set<T> := set e: T | e in xs;
      assert xs[0] in s;
    }
    forall i | 0 <= i < n ensures xs[i] in xs { }
    calc { n; == |xs|; }
    var t :| t == r;
    r := t;
    assert n == |xs| by { reveal Op(); }
  }
  opaque predicate Op() { true }
  method Fail(a: Res<int>) returns (b: Res<int>) {
    var x :- a;
    b := Ok(x + 1);
  }
  function FailE(a: Res<int>): Res<int> {
    var x :- a; Ok(x + 1)
  }
  least predicate Even(n: nat) { n == 0 || (n >= 2 && Even(n - 2)) }
  least lemma EvenL(n: nat) requires Even(n) ensures n % 2 == 0 { }
  greatest predicate Inf(n: nat) { Inf(n + 1) }
  twostate function Two(o: C<int>): int reads o { old(o.f) + o.f }
  lemma Ind(n: nat, m: nat) ensures n + m >= n { }
  function Nested(a: int): int {
    var f := (b: int) => a + b;
    f(a) + (var a2 := a; a2)
  }
  iterator It(x: int) yields (y: int) { yield x; }
  newtype N = x: int | 0 <= x < 10
  type Sub<T> = s: seq<T> | |s| <= 3
  method Mod(c: C<int>, v: int) modifies c ensures c.f == v { c.f := v; }
}
module B refines A {
  class C<W(0)> ... {
    constructor (w: W)
    method Locals(p: W) returns (out: W)
    static function Pick<V2>(opt: Opt<V2>, dflt: V2): V2
  }
  function F<E>(elem: E, count: nat): (res: seq<E>)
  method Stmts<E(==)>(list: seq<E>, dflt: E) returns (last: E, len: nat)
  method Fail(input: Res<int>) returns (output: Res<int>)
  function FailE(input: Res<int>): Res<int>
  least predicate Even(k: nat)
  least lemma EvenL(k: nat)
  greatest predicate Inf(k: nat)
  twostate function Two(obj: C<int>): int
  lemma Ind(i: nat, j: nat)
  function Nested(z: int): int
  method Mod(obj: C<int>, val: int)
}
