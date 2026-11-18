// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings --show-proof-obligation-expressions "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: where an expression with a binding is shown at other
// arguments than it was written with, such as a precondition at a call, the lemma is shown at those arguments.

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

ghost function NeedsFind(n: nat): int
  requires (var m := Find(n); m) > 5
{
  0
}

ghost function NeedsSplit(n: nat): int
  requires (var a, b := Split(n); b) > 5
{
  0
}

ghost function NeedsAdd(n: int): int
  requires (var z := Add(y := 2, x := n); z) + (var w := Add(n); w) > 100
{
  0
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

  ghost function NeedsMine(n: int): int
    requires (var m := Mine(n); m) > 5
  {
    0
  }

  ghost function NeedsStatic(n: int): int
    requires (var m := Static(n); m) > 5
  {
    0
  }
}

method Calls(x: nat, y: nat, t: nat, u: int, v: int, c: C) {
  ghost var a := NeedsFind(x + 1);  // error: precondition
  ghost var b := NeedsSplit(y + 2);  // error: precondition
  ghost var d := NeedsAdd(t + 3);  // error: precondition
  ghost var e := c.NeedsMine(u + 4);  // error: precondition
  ghost var f := c.NeedsStatic(v + 5);  // error: precondition
}
