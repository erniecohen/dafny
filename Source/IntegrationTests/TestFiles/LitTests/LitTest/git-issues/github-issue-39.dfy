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
