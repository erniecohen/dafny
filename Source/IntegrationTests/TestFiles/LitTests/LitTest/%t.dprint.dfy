// DirtyLoops.dfy

method L0(n: nat)
{
  var i, j := 0, 2;
  while i < n
    invariant 0 <= i <= n
  assert i == n;
  assert j == 2;
  assert n == 0;
}

method L1(n: nat) returns (i: int, j: int)
{
  i, j := 0, 2;
  while i < n
    invariant 0 <= i <= n
  assert i == n;
  assert j == 2;
  assert n == 0;
}

method L2(n: int) returns (r: int, s: int)
{
  r := s;
  var i := 0;
  while i < n
  assert r == s;
  assert i == 0;
}

method L3(n: int) returns (r: int, s: int)
{
  var kn, kr, ks := n, r, s;
  var i := 0;
  while i < n + s
  assert kn == n;
  assert i == 0;
  assert kr == r;
  assert ks == s;
}

method L4(a: array<int>, n: int)
    returns (r: int, s: int)
  modifies a
{
  var ks := s;
  r := n;
  var i := 0;
  while i < n + s
    modifies if r < 3 then {a} else {}
  assert i == 0;
  assert r == n;
  assert s == ks;
}

method L5(a: array<int>, n: int)
    returns (r: int, s: int)
{
  var ks := s;
  r := n;
  var i := 0;
  while i < n + s
    decreases r
  assert r == n;
  assert s == ks;
}

method L6()
{
  var i, j := 0, 2;
  while false
  assert i == 0;
  assert j == 0;
}

method M0(a: array<int>, n: nat)
  requires a.Length == 10
  modifies a
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n
  assert i == n;
  assert a[0] == old(a[0]);
  assert n == 0;
}

method M1(a: array<int>, b: array<int>, n: nat)
  requires a != b && a.Length == 10
  modifies a
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n && a != b
  assert i == n;
  assert a[0] == old(a[0]);
  assert n == 0;
}

method M2(a: array<int>, b: array<int>, n: nat)
  requires a != b && 10 <= a.Length == b.Length
  modifies a
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n && a != b
    invariant a[0] == old(a[0])
  assert i == n;
  assert a[0] == old(a[0]);
  assert b[1] == old(b[1]);
  assert a[1] == old(a[1]);
  assert n == 0;
}

method M3(a: array<int>, b: array<int>, n: nat)
  requires a != b && 10 <= a.Length == b.Length
  modifies a
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n && a != b
    invariant b[0] == old(b[0])
  assert i == n;
  assert a[0] == old(a[0]);
  assert b[1] == old(b[1]);
  assert n == 0;
}

method M4(a: array<int>, b: array<int>, n: nat)
  requires a != b && 10 <= a.Length == b.Length
  modifies a, b
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n && a != b
    invariant a[0] == old(a[0])
  assert i == n;
  assert a[0] == old(a[0]);
  assert b[1] == old(b[1]);
  assert a[1] == old(a[1]);
}

method M5(a: array<int>, b: array<int>, n: nat)
  requires a != b && 10 <= a.Length == b.Length
  modifies a, b
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n && a != b
    invariant a[0] == old(a[0])
    modifies a
  assert i == n;
  assert a[0] == old(a[0]);
  assert b[1] == old(b[1]);
  assert a[1] == old(a[1]);
  assert n == 0;
}

method M6(a: array<int>, b: array<int>, n: nat)
  requires a != b && 10 <= a.Length == b.Length
  modifies a, b
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n && a != b
    modifies a
  assert i == n;
  assert a[0] == old(a[0]);
  assert b[1] == old(b[1]);
  assert a[1] == old(a[1]);
  assert n == 0;
}

method M7(c: C, n: nat)
  modifies c
{
  var i := 0;
  while i < n
    invariant 0 <= i <= n
    invariant unchanged(c)
  assert i == n;
  assert c.y == old(c.y);
  assert n == 0;
}

method M8(c: C, n: nat)
  modifies c
{
  var i := 0;
  label L:
  while i < n
    invariant 0 <= i <= n
    invariant unchanged@L(c)
  assert i == n;
  assert c.y == old(c.y);
  assert n == 0;
}

method M9(c: C, n: nat)
  modifies c
{
  var k := c.y;
  var i := 0;
  while i < n
    invariant 0 <= i <= n
    invariant k == old(c.y)
  assert k == old(c.y);
  assert k == c.y;
  assert n == 0;
}

method M10(c: C)
  modifies c
{
  var k := c.y;
  var i := 0;
  while i < 100
    decreases c.m
  assert k == c.y;
}

method M11(c: C)
  modifies c
{
  var i := 0;
  while i < c.u
  assert c.y == old(c.y);
  assert i == c.u;
}

method M12(c: C)
  modifies c
{
  var i := 0;
  while i < c.y
  assert c.y == old(c.y);
}

method M13(c: C)
  modifies c
{
  var i := 0;
  while i < 7
    invariant allocated(c)
  assert c.y == old(c.y);
}

method H0a() returns (k: C)
{
  var i := 0;
  k := new C;
  var k0 := k;
  while i < 7
    invariant fresh(k)
  assert fresh(k);
  assert k == k0;
  assert i == 7;
}

method H0b() returns (k: C)
{
  var i := 0;
  var c := new C;
  k := new C;
  var k0 := k;
  while i < 7
    invariant fresh(k)
  assert fresh(k);
  assert k == k0 || k == c;
  assert k == k0;
  assert i == 7;
}

method H0c() returns (k: C)
{
  var i := 0;
  k := new C;
  var k0 := k;
  while i < 7 && k.y == k.y
    invariant fresh(k)
  assert fresh(k);
  assert k == k0;
  assert i == 7;
}

method H1(a: array<int>) returns (k: C)
  requires a.Length == 10
  modifies a
{
  var i := 0;
  k := new C;
  k.y := 12;
  while i < 7
    invariant a[0] == old(a[0])
  assert k.y == 12;
}

method H2(a: array<int>) returns (k: C)
  requires a.Length == 10 && a[0] == 8
  modifies a
{
  var i := 0;
  k := new C;
  label L:
  k.y := 12;
  while i < 7
    invariant old(a[0]) == 8 == old@L(a[0])
  assert k.y == 12;
}

method H3(a: array<int>) returns (k: C)
  requires a.Length == 10 && a[0] == 8
{
  var i := 0;
  k := new C;
  k.y := 12;
  while i < 7
    invariant i <= 7
  assert k.y == 12;
}

method H4(a: array<int>) returns (k: C)
  requires a.Length == 10 && a[0] == 8
{
  var i := 0;
  k := new C;
  k.y := 12;
  while i < 7
    invariant k.y % 2 == 0
  assert k.y == 12;
}

method H5() returns (k: C)
{
  var i := 0;
  k := new C;
  k.y := 12;
  var c := k;
  while i < 7
    invariant k.y % 2 == 0
    modifies {}
  assert c.y == 12;
  assert k.y == 12;
}

method H6(c: C) returns (k: C)
  requires c.y == 3
{
  k := c;
  var i := 0;
  while i < 7
    invariant k.y == 3
    modifies {}
  assert old(allocated(k));
}

method H7(c: C) returns (k: C)
  requires c.u == 3
{
  k := c;
  var i := 0;
  while i < 7
    invariant k.u == 3
  assert old(allocated(k));
}

method E0(c: C) returns (k: C)
  modifies c
{
  k := c;
  var i := 0;
  while i < 7
    invariant k.y == 3
    modifies c
}

method W0(c: C) returns (k: C?)
  requires c.y == 3
  modifies c
{
  k := c;
  var i := 0;
  while i < 7
    invariant k.y == 3
}

method F0(S: set<int>, y: int)
{
  forall s | s in S
    ensures s < 0
  assert y in S ==> y < 0;
}

method F1(S: set<int>, y: int)
{
  forall s | s in S
    ensures s < 0
  assert y in S ==> y == -18;
}

ghost predicate P(y: int)

method F2()
{
  forall y: int | true
    ensures P(y)
  forall x: int | true
}

method GuardAlwaysHoldsOnEntry_BodyLessLoop()
{
  var x := 20;
  while x < 20
    invariant x % 2 == 0
  assert x == 20;
}

method GuardAlwaysHoldsOnEntry_LoopWithBody()
{
  var x := 20;
  while x < 20
    invariant x % 2 == 0
  {
    assert false;
  }
  assert x == 20;
}

class C {
  var y: int
  var m: nat
  const u: int
}

class FineGrained {
  var f: int
  var g: int

  method R0(x: int)
    requires f <= x
    modifies `f
  {
    while 0 < f
      invariant f <= x
    assert g == old(g);
  }

  method R1(x: int)
    requires f <= x
    modifies this
  {
    while 0 < f
      invariant f <= x
      modifies `f
    assert g == old(g);
  }

  method R2(x: int)
    requires f <= x
    modifies this
  {
    while 0 < f
      invariant f <= x
      decreases f
      modifies `f
    {
      var i, prev := 0, f;
      while i < 7
        modifies this
      f := prev - 1;
    }
  }

  method R3(x: int)
    requires f <= x
    modifies this
  {
    while 0 < f
      invariant f <= x
      decreases f
      modifies {}
    {
      var i := 0;
      while i < 7
        modifies `f
      assert g == old(g);
      f := f - 1;
    }
  }

  method R4(c: FineGrained, x: int)
    requires this != c
    modifies this, c
  {
    var i := 0;
    while i < 7
      invariant i <= 7
      modifies {}
    {
      var j := 0;
      while j < 7
        modifies c
      assert g == old(g);
      i := i + 1;
    }
  }
}
