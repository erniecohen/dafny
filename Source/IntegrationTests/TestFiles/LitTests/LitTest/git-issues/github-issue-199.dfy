// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: under --lemma-bindings, an expression can bind
// the results of a lemma, "var y1, ..., yk := L(args); e".  The call is checked as a call
// statement is.  In e, and wherever e is used, the variables denote results that satisfy L's
// postcondition, and they are a fixed function of the lemma and its arguments.  Nothing says which
// of the results that the postcondition allows they are.
//
// Each section has the facts that must be provable, and facts that must not be.  The last lemma of
// the file uses every function that binds a result and then asserts false.

// ---------- The program of the issue

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

ghost function Above(n: nat): nat {
  var m := Find(n); m
}

lemma AboveIsAbove(n: nat)
  ensures Above(n) > n
{
}

lemma NotTheBody(n: nat)
  ensures Above(n) == n + 1  // error: the result is one the postcondition allows, not the one the body computes
{
}

// ---------- The type of a result

lemma Any() returns (m: nat)
{
  m := 0;
}

ghost function AnyResult(): int {
  ghost var m := Any(); m
}

lemma AnyResultIsNat()
  ensures AnyResult() >= 0
{
}

lemma AnyResultIsNotKnown()
  ensures AnyResult() == 0  // error
{
}

type Small = x: int | 0 <= x < 10

lemma SmallOne(n: int) returns (s: Small)
{
  s := 3;
}

lemma SmallIsSmall(n: int)
  ensures (var s := SmallOne(n); s) < 10
{
}

// ---------- Several results are chosen together

lemma Split(n: nat) returns (a: nat, b: nat)
  ensures a + b == n
{
  a, b := 0, n;
}

ghost function Sum(n: nat): nat {
  var a, b := Split(n); a + b
}

ghost function Second(n: nat): nat {
  var _, b := Split(n); b
}

lemma SumIs(n: nat)
  ensures Sum(n) == n
  ensures Second(n) <= n
{
}

lemma SplitIsNotKnown(n: nat)
  ensures Second(n) == n  // error
{
}

// ---------- The same lemma at equal arguments gives the same results

ghost function A(n: nat): nat {
  var m := Find(n); m
}

ghost function B(n: nat): nat {
  var m := Find(n); m
}

lemma SameChoice(n: nat)
  ensures A(n) == B(n)
{
}

ghost function Twice(n: nat): int {
  var a := Find(n);
  var b := Find(n + 0);
  10 / (1 + a - b)  // well-formed, since a == b
}

ghost function TwiceApart(n: nat): int {
  var a := Find(n);
  var b := Find(n + 1);
  10 / (1 + a - b)  // error: possible division by zero
}

lemma DifferentArguments(n: nat)
  ensures A(n) != A(n + 1)  // error
{
}

// ---------- A call statement's results are not the bound results

lemma Exact(n: nat) returns (m: nat)
  ensures m == n + 1
{
  m := n + 1;
}

lemma Statements(n: nat) {
  var a := Find(n);
  var b := Find(n);
  ghost var c := A(n);
  ghost var d := (var m := Find(n); m);
  assert c == d;
  if * {
    assert a == b;  // error: two calls may return different results
  } else if * {
    assert a == c;  // error: a call's result is not the bound result
  } else {
    var e := Exact(n);
    assert e == (var m := Exact(n); m);  // the postcondition allows one result only
  }
}

// ---------- Under lambdas and quantifiers, nested, and in specifications

ghost function Table(n: nat): seq<nat> {
  seq(n, i requires 0 <= i => var m: nat := Find(i); m)
}

lemma TableEntry(n: nat)
  requires n > 0
  ensures Table(n)[0] > 0
{
  var k := (var m := Find(0); m);
}

lemma AllAbove()
  ensures forall n: nat :: (var m := Find(n); m) > n
{
}

lemma NotAllAboveByTwo()
  ensures forall n: nat :: (var m := Find(n); m) > n + 1  // error
{
}

ghost function Nested(n: nat): nat {
  var m := Find(n);
  var k := Find(m);
  k
}

lemma NestedAbove(n: nat)
  ensures Nested(n) > n + 1
{
}

ghost function WithEnsures(n: nat): nat
  ensures var m := Find(n); WithEnsures(n) == m
{
  var m := Find(n); m
}

ghost predicate WithRequires(n: nat)
  requires (var m := Find(n); m) > n
{
  true
}

ghost function WithDecreases(n: nat): nat
  decreases var m := Find(n); m
{
  if n == 0 then 0 else WithDecreases(n - 1)  // error: the results at n - 1 and at n are not related
}

lemma Outer(n: nat) returns (r: nat)
  ensures var m := Find(n); r > m
{
  var m := Find(n);
  r := (var k := Find(n); k) + 1;
}

ghost function BindOuter(n: nat): nat {
  var r := Outer(n); r
}

lemma BindOuterAbove(n: nat)
  ensures BindOuter(n) > n + 1
{
}

ghost function Lambda(): int -> nat {
  n => var m := Find(if n >= 0 then n else 0); m
}

ghost function Comprehension(): set<nat> {
  set n: nat | n < 3 :: (var m := Find(n); m)
}

lemma InComprehension()
  ensures A(2) in Comprehension()
  ensures 0 !in Comprehension()
{
  assert A(2) == (var m := Find(2); m);
}

ghost const Constant: nat := var m := Find(7); m

lemma ConstantAbove()
  ensures Constant > 7
{
}

type AboveThree = x: int | (var m := Find(3); x < m) witness 0

ghost function Default(n: nat, k: nat := (var m := Find(n); m)): nat {
  k
}

lemma DefaultAbove(n: nat)
  ensures Default(n) > n
{
}

method InMethod(n: nat) returns (r: nat)
  ensures r > n
{
  ghost var m := (var m := Find(n); m);
  assert m > n;
  r := n + 1;
  assert var k := Find(r); k > n + 1;
}

// ---------- Type parameters, function results, receivers, named and default arguments

lemma Id<T>(x: T) returns (y: T)
  ensures y == x
{
  y := x;
}

ghost function IdInt(n: int): int {
  var y := Id(n); y
}

ghost function IdAt<T(!new)>(x: T): T {
  var y := Id<T>(x); y
}

datatype Pair = Pair(fst: int, snd: bool)

lemma IdIs(n: int, p: Pair)
  ensures IdInt(n) == n && IdAt(n) == n && IdAt(p) == p
{
}

lemma Increasing() returns (f: nat -> nat)
  ensures forall n: nat :: f(n) > n
{
  f := n => n + 1;
}

ghost function UseIncreasing(n: nat): nat {
  var f := Increasing(); f(n)
}

lemma UseIncreasingAbove(n: nat)
  ensures UseIncreasing(n) > n
{
}

class C {
  const k: int

  lemma Mine(n: int) returns (m: int)
    ensures m == n + k
  {
    m := n + k;
  }

  ghost function G(n: int): int {
    var m := Mine(n); m
  }

  ghost function H(n: int): int {
    var m := this.Mine(n); m
  }

  lemma Loose(n: int) returns (m: int)
    ensures m > n
  {
    m := n + 1;
  }

  static lemma Static(n: int) returns (m: int)
    ensures m == n
  {
    m := n;
  }
}

ghost function OnC(c: C, n: int): int {
  var m := c.Mine(n); m
}

ghost function OnStatic(c: C, n: int): int {
  var m := C.Static(n);
  var k := c.Static(m);
  k
}

lemma Receivers(c: C, d: C, n: int)
  ensures OnC(c, n) == n + c.k
  ensures c.G(n) == c.H(n) == OnC(c, n)
  ensures OnStatic(c, n) == n
  ensures (var m := c.Loose(n); m) == (var m := c.Loose(n); m)
{
}

lemma ReceiverIsAnArgument(c: C, d: C, n: int)
  ensures (var m := c.Loose(n); m) == (var m := d.Loose(n); m)  // error
{
}

trait Tr {
  lemma L(n: nat) returns (m: nat)
    ensures m > n

  ghost function F(n: nat): nat {
    var m := L(n); m
  }
}

class D extends Tr {
  lemma L(n: nat) returns (m: nat)
    ensures m > n
    ensures m == n + 2
  {
    m := n + 2;
  }
}

lemma ThroughTrait(t: Tr, n: nat)
  ensures t.F(n) > n
{
}

lemma TraitAndClass(d: D, n: nat) {
  var a := (var m := d.L(n); m);
  assert a == n + 2;
  var t: Tr := d;
  var b := (var m := t.L(n); m);
  assert b > n;
  assert a == b;  // error: the lemma of the trait and the lemma of the class are different lemmas
}

lemma Add(n: nat, k: nat := 1) returns (m: nat)
  ensures m == n + k
{
  m := n + k;
}

lemma NamedAndDefault(n: nat)
  ensures (var m := Add(n); m) == n + 1
  ensures (var m := Add(k := 2, n := n); m) == n + 2
  ensures (var m := Add(n, 3); m) == (var m := Add(k := 3, n := n); m)
{
}

// ---------- The precondition is checked at the binding, and the arguments are evaluated once

lemma Positive(n: int) returns (m: int)
  requires n > 0
  ensures 0 < m <= n
{
  m := n;
}

ghost function UsePositive(n: int): int
  requires n > 0
{
  var m := Positive(n); m
}

ghost function NoPrecondition(n: int): int {
  var m := Positive(n); m  // error: precondition
}

ghost function Argument(n: nat, d: nat): nat {
  var m := Find(n / d); m  // error: possible division by zero (reported once)
}

ghost function Guarded(n: int): int {
  if n > 0 then (var m := Positive(n); m) else 0
}

lemma GuardedBounds(n: int)
  ensures 0 <= Guarded(n)
  ensures n > 0 ==> Guarded(n) <= n
{
}

// ---------- A lemma without a body is trusted, as it is when it is called

lemma {:axiom} Root(n: nat) returns (m: nat)
  ensures m * m <= n < (m + 1) * (m + 1)

ghost function UseRoot(n: nat): nat {
  var m := Root(n); m
}

lemma UseRootBounds(n: nat)
  ensures UseRoot(n) * UseRoot(n) <= n
{
}

// ---------- Nothing is known about the results at arguments where no binding has been checked

ghost predicate Q(n: int)

lemma {:axiom} EstablishQ(n: int) returns (m: int)
  requires n > 0
  ensures Q(n) && m == 0

ghost function BindQ(n: int): int {
  if n > 0 then (var m := EstablishQ(n); m) else 0
}

lemma QOnlyWhereChecked(n: int) {
  var x := BindQ(n);
  if n > 0 {
    assert Q(n);
  } else {
    assert Q(n);  // error
  }
}

// ---------- The whole postcondition holds where the results are used

ghost predicate Fact(n: int)

lemma {:axiom} WithFact(n: int) returns (m: int)
  ensures Fact(n)
  ensures m > n

ghost function UseWithFact(n: int): int {
  var m := WithFact(n); m
}

lemma FactHolds(n: int)
  ensures Fact(n)
{
  var x := UseWithFact(n);
}

// ---------- Recursion through a binding that decreases

ghost function Even(n: nat): bool
  decreases n, 1
{
  var b := EvenLemma(n); b
}

lemma EvenLemma(n: nat) returns (b: bool)
  decreases n, 0
  ensures b == (n % 2 == 0)
{
  if n == 0 {
    b := true;
  } else {
    b := !Even(n - 1);
  }
}

lemma EvenIs()
  ensures Even(10) && !Even(7)
{
}

// ---------- None of the above proves false

lemma Vacuity(n: nat, c: C, t: Tr)
  requires n > 0
{
  var x := Above(n) + AnyResult() + Sum(n) + Second(n) + A(n) + B(n) + Twice(n) + |Table(n)| + Nested(n)
    + WithEnsures(n) + BindOuter(n) + Lambda()(n) + |Comprehension()| + Constant + Default(n) + IdInt(n)
    + IdAt(n) + UseIncreasing(n) + c.G(n) + c.H(n) + OnC(c, n) + OnStatic(c, n) + t.F(n) + UsePositive(n)
    + Guarded(n) + UseRoot(n) + BindQ(n) + UseWithFact(n) + (if Even(n) then 1 else 0);
  var s: AboveThree := 0;
  assert WithRequires(n);
  assert false;  // error
}
