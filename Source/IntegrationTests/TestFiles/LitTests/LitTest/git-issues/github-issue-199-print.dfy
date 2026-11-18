// RUN: %resolve --lemma-bindings --print:- "%s" > "%t"
// RUN: %resolve --lemma-bindings --rprint:"%t.dfy" "%s" >> "%t"
// RUN: %verify --lemma-bindings "%t.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: a binding of a lemma's results is printed as it was written.
// The program printed after resolution, which includes the trigger that was selected for the quantifier in
// InAQuantifier, resolves and verifies again.

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

lemma Split(n: nat) returns (a: nat, b: nat)
  ensures a + b == n
{
  a, b := 0, n;
}

lemma Add(x: int, y: int := 1) returns (z: int)
  ensures z == x + y
{
  z := x + y;
}

class C {
  const k: int

  lemma Mine(n: int) returns (m: int)
    ensures m == n + k
  {
    m := n + k;
  }

  static lemma Static(n: int) returns (m: int)
    ensures m == n
  {
    m := n;
  }

  ghost function ImplicitReceiver(n: int): int {
    var m := Mine(n); m
  }
}

ghost function One(n: nat): nat {
  var m := Find(n); m
}

ghost function WithGhost(n: nat): nat {
  ghost var m := Find(n); m
}

ghost function WithType(n: nat): nat {
  var m: nat := Find(n + 1); m
}

ghost function Two(n: nat): nat {
  var a, b := Split(n); a + b
}

ghost function Wildcard(n: nat): nat {
  var _, b := Split(n); b
}

ghost function Named(n: int): int {
  var z := Add(y := 2, x := n); var w := Add(n); z + w
}

ghost function Receivers(c: C, n: int): int {
  var m := c.Mine(n); var k := C.Static(m); var j := c.Static(k); j
}

ghost function Nested(n: nat): nat {
  var m := Find(var k := Find(n); k); m
}

lemma InAQuantifier()
  ensures forall n: nat :: (var m := Find(n); m) > n
{
}

lemma Uses(c: C, n: nat)
  ensures One(n) > n && Two(n) == n && Named(n) == 2 * n + 3 && Receivers(c, n) == n + c.k && Nested(n) > n + 1
{
}
