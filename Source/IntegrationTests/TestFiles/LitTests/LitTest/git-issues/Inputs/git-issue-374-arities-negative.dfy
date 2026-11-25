// Each lemma has one error with --function-extensionality-axiom.
lemma NullaryDistinct() {
  var f := () => 0;
  var g := () => 1;
  assert f == g;
}
lemma BinaryDistinct() {
  var f := (x: int, y: bool) => x;
  var g := (x: int, y: bool) => x + 1;
  assert f == g;
}
// Agreement on a proper subdomain does not equate the original values.
lemma WiderDomain() {
  var f: (nat, bool) -> int := (x: int, y: bool) => if x < 0 then 7 else x;
  var g: (nat, bool) -> int := (x: int, y: bool) => x;
  assert forall x: nat, y: bool :: f(x, y) == g(x, y);
  assert f == g;
}
lemma Generic<A, B>(f: (A, bool) -> B, g: (A, bool) -> B)
  requires forall x, y :: f(x, y) == g(x, y)
  ensures f == g
{}
// Partial and heap-reading functions still have more than their result map.
lemma Partial(f: (int, bool) --> int, g: (int, bool) --> int)
  requires forall x, y :: f.requires(x, y) && g.requires(x, y) ==> f(x, y) == g(x, y)
  ensures f == g
{}
lemma Reading(f: () ~> int, g: () ~> int)
  requires f.requires() && g.requires()
  requires f() == g()
  ensures f == g
{}

// Live extensionality facts must not make the context inconsistent.
lemma VacuityNullary<B>(f: () -> B, g: () -> B)
  requires f() == g()
{
  assert f == g;
  assert false;
}
lemma VacuityBinary<B>(f: (int, bool) -> B, g: (int, bool) -> B)
  requires forall x, y :: f(x, y) == g(x, y)
{
  assert f == g;
  assert false;
}
lemma VacuityFour(f: (int, real, bool, ORDINAL) -> char,
                  g: (int, real, bool, ORDINAL) -> char)
  requires forall a, b, c, d :: f(a, b, c, d) == g(a, b, c, d)
{
  assert f == g;
  assert false;
}
lemma VacuitySubset() {
  var f := (x: nat, y: nat) => x + y;
  var g := (x: nat, y: nat) => y + x;
  assert f == g;
  assert false;
}

datatype D = A | B
datatype Other = Other
function Named(x: D, y: nat): int { if x.A? then y else 0 }
lemma VacuityNamed() {
  var f := (x: D, y: nat) => if x.B? then 0 else y;
  var g: (D, nat) -> int := Named;
  assert forall x: D, y: nat :: f(x, y) == g(x, y);
  assert f == g;
  assert false;
}

datatype Pair = Pair(x: int, b: bool)
function MakePair(x: int, b: bool): Pair { Pair(x, b) }
lemma VacuityConstructor() {
  var f := (x: int, b: bool) => Pair(x, b);
  var g: (int, bool) -> Pair := MakePair;
  assert forall x: int, b: bool :: f(x, b) == g(x, b);
  assert f == g;
  assert false;
}

function Nonnegative(x: nat, b: bool): bool ensures x >= 0 { true }
lemma VacuityRequires() {
  var f: (nat, bool) -> bool := Nonnegative;
  var r: (nat, bool) -> bool := f.requires;
  assert r == Nonnegative;
  assert false;
}

type Empty = x: int | false witness *
lemma VacuityEmpty() {
  var f: (Empty, Empty) -> int := (x: Empty, y: int) => 0;
  var g: (Empty, Empty) -> int := (x: int, y: Empty) => 1;
  assert f == g;
  assert false;
}
