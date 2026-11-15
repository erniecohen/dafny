// RUN: %exits-with 4 %verify --type-system-refresh:false --general-newtypes:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// A static member called through an object discards the object, but the
// expression that computes it must still be checked, inside a match as outside.

datatype D = A | B
datatype Box = Box(c: C, n: int)
class C {
  static function f(): int { 0 }
  static method m() returns (r: int) { r := 0; }
}
function Id(c: C): C requires false { c }
function Pos(c: C, x: int): C requires x > 0 { c }

// Each receiver has an obligation that fails.

method Plain(c: C) {
  var r := (assert false; c).f();  // error, as before
}

method AssertInReceiver(d: D, c: C) {
  match d
  case A => var r := (assert false; c).f();  // error
  case B =>
}

method PreconditionInReceiver(d: D, c: C) {
  match d
  case A => var r := Id(c).f();  // error
  case B =>
}

method IndexInReceiver(d: D, s: seq<C>, i: int) {
  match d
  case A => var r := s[i].f();  // error
  case B =>
}

method StaticMethod(d: D, c: C) {
  match d
  case A => var r := (assert false; c).m();  // error
  case B =>
}

method Nested(d: D, e: D, c: C) {
  match d {
    case A =>
      match e {
        case A => var r := (assert false; c).f();  // error
        case B =>
      }
    case B =>
  }
}

method CaseVariable(b: Box) {
  match b
  case Box(c, n) => var r := (assert n > 0; c).f();  // error
}

function InMatchExpression(d: D, c: C): int {
  match d
  case A => (assert false; c).f()  // error
  case B => 0
}

// Controls: each receiver's obligation holds, so these verify.

method SafeAssert(d: D, c: C) {
  match d
  case A => var r := (assert d == A; c).f(); var q := (assert d == A; c).m();
  case B =>
}

method SafePrecondition(d: D, c: C, x: int) requires x > 0 {
  match d
  case A => var r := Pos(c, x).f(); var q := Pos(c, x).m();
  case B =>
}

method SafeIndex(d: D, s: seq<C>, i: int) requires 0 <= i < |s| {
  match d
  case A => var r := s[i].f();
  case B =>
}

method SafeCaseVariable(b: Box) requires b.n > 0 {
  match b
  case Box(c, n) => var r := (assert n > 0; c).f();
}

function SafeInMatchExpression(d: D, c: C): int {
  match d
  case A => (assert d == A; c).f()
  case B => 0
}

// Quantifiers, comprehensions and lambdas substitute their bound variables
// into the body before checking it, which must keep the receiver too.

lemma InForall(c: C)
  ensures forall x: int :: (assert false; c).f() == 0  // error
{
}

method InExists(c: C) {
  ghost var b := exists x: int :: (assert false; c).f() == x;  // error
}

method InSetComprehension(c: C) {
  var s := set x | 0 <= x < 3 :: (assert false; c).f();  // error
}

method InMapComprehension(c: C) {
  var m := map x | 0 <= x < 3 :: (assert false; c).f();  // error
}

method InLambda(c: C) {
  var g := (x: int) => (assert x > 0; c).f();  // error
}

method IndexInForall(s: seq<C>) {
  ghost var b := forall i | 0 <= i <= |s| :: s[i].f() == 0;  // error
}

// Controls: each receiver's obligation holds, so these verify.

lemma SafeForall(c: C)
  ensures forall x: int :: (assert x == x; c).f() == 0
{
}

method SafeBinders(s: seq<C>, c: C) {
  ghost var b := forall i | 0 <= i < |s| :: s[i].f() == 0;
  ghost var e := exists x: int :: (assert x == x; c).f() == x;
  var t := set x | 0 <= x < 3 :: (assert 0 <= x; c).f();
  var m := map x | 0 <= x < 3 :: (assert x < 3; c).f();
  var g := (x: int) requires x > 0 => (assert x > 0; c).f();
}

// A greatest lemma's body is checked in its prefix lemma, whose body is
// cloned from the syntax and resolved again.

greatest predicate P(x: int) { true }

greatest lemma InGreatestLemma(c: C, x: int)
  ensures P(x)
{
  var r := (assert false; c).f();  // error
}

greatest lemma SafeGreatestLemma(c: C, x: int)
  ensures P(x)
{
  var r := (assert x == x; c).f();
}
