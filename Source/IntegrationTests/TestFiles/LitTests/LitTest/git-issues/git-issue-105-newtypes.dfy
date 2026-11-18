// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of newtypes (section 3.5 of
// docs/dev/issue105-member-invariants-plan.md).  A newtype N with member invariants is a subset of
// "raw N", whose values are those of the base type.  A constraint in the header, in the old syntax,
// moves into N as one more conjunct, which keeps its own message.  The result of an operation is a
// value of N, checked against each invariant, unless a leaf other than a literal is raw, when the whole
// operation is raw.  The vacuity control V1 is that the invariants do not make N empty.  Each check
// has a negative control, marked "error".

module MemberForm {
  newtype Even = int {
    invariant IsEven()
    witness 0
    ghost predicate IsEven() { this as int % 2 == 0 }
  }

  method Add(a: Even, b: Even) returns (c: Even) {
    c := a + b;
    c := a + 2;
    c := -a;
    c := 2;
  }

  // The where clause of a parameter of type Even gives the body of the predicate.
  method Where(e: Even) { assert e as int % 2 == 0; }

  // V1: the invariant does not make Even empty.
  method V1(e: Even) { assert false; } // error

  method AddOdd(a: Even) returns (c: Even) {
    c := a + 1; // error: the invariant might not hold
  }

  // The literal 3 has the type of the other operand, Even, so it is checked, even where the
  // result is not.
  method Literal(a: Even) returns (b: bool) {
    b := a < 3; // error: the invariant might not hold
  }

  method Subterm(a: Even) returns (c: Even) {
    c := a + 3 - 1; // error: a + 3 is a value of Even
  }

  // An operand of "raw Even" makes the operation raw, and nothing in it is checked.
  ghost method Raw(a: Even, r: raw Even) returns (b: bool) {
    b := r < 3;
    var s := r + 3 - 1;
    var t := (a + 3) * r;
    var u := 3 as raw Even + 4;
  }

  ghost method RawToEven(r: raw Even) returns (c: Even) {
    c := r + 1; // error: the invariant might not hold
  }

  ghost method RawChecked(r: raw Even) returns (c: Even) requires r.IsEven() {
    c := r + 2;
  }
}

module OldSyntax {
  // The header constraint is one more conjunct of Small, with the message of the old syntax.
  newtype Small = x: int | x < 10 {
    invariant this as int > 0
    witness 1
  }

  method Ok() returns (s: Small) { s := 5; }
  method BelowInvariant() returns (s: Small) { s := 0; } // error: the invariant might not hold
  method AboveHeader() returns (s: Small) { s := 12; } // error: the newtype constraint might not hold
  method Where(s: Small) { assert 0 < s as int < 10; }
  method V1(s: Small) { assert false; } // error
}

module Members {
  newtype Even = x: int | true {
    invariant IsEven()
    witness 0
    ghost predicate IsEven() { (this as int) % 2 == 0 }
    function Half(): int { (this as int) / 2 }
    function Next(): Even { this + 2 }
    function Bad(): Even { this + 1 } // error: the invariant might not hold
    static function Zero(): Even { 0 }
    method Inc() returns (e: Even) ensures e == this + 2 { e := this + 2; }
  }

  // IsEven is a raw member, since the invariant uses it, and Half is not, so a raw receiver of Half
  // must satisfy the invariant.
  ghost method UseRaw(e: raw Even) { var b := e.IsEven(); var h := e.Half(); } // error: receiver of Half
  ghost method UseRaw2(e: raw Even) requires e.IsEven() { var h := e.Half(); var n := e.Next(); var z := Even.Zero(); }
  method Use(e: Even) returns (f: Even) ensures f.IsEven() { f := e.Next(); f := f.Inc(); }
}

module Traits {
  // An invariant of a trait is one more conjunct of a newtype that extends it.
  trait G { const k: int := 1  invariant k != 0 }
  newtype Small extends G = x: int | x < 10 {
    invariant this as int > 0
    witness 1
    function Twice(): int { 2 * (this as int) }
  }
  method Use(s: Small) returns (t: int) ensures t > 0 {
    t := s.Twice();
    var g: G := s;
    assert g.k != 0;
  }
  // From "raw Small" to G, the conversion checks every conjunct of Small.
  ghost method ToTrait(r: raw Small) returns (g: G) { g := r; } // error: the invariant might not hold
  ghost method ToTraitChecked(r: raw Small) returns (g: G) requires 0 < r as int < 10 { g := r; }

  trait Zero { const k: int := 0  invariant k != 0 }
  newtype Never extends Zero = x: int | x < 10 {
    invariant this as int > 0
    witness 1 // error: the invariant of Zero might not hold
  }
  // Never is empty.  As for an empty subset type, or an empty newtype in the old syntax, the verifier
  // assumes the where clause of a local variable, so nothing after the witness error is reported.
  method Six() { var s: Never := 6; }
}

abstract module A {
  newtype N = x: int | 0 <= x < 100 { invariant this as int != 5  witness 1 }
  method Inc(n: N) returns (m: N) requires n as int < 50 && n as int != 4 { m := n + 1; }
}

module B refines A {
  // A refinement adds an invariant.
  newtype N ... { invariant this as int != 6 }
  method Six() { var a: N := 6; } // error: the invariant of B might not hold
  method Seven() { var a: N := 7; }
}

module E {
  export provides N, Make
  newtype N = int { invariant this as int > 0  witness 1 }
  function Make(): N { 3 }
}

module Client {
  import E
  method C() returns (n: E.N) { n := E.Make(); }
}

module OtherBases {
  newtype R = real { invariant this as real > 0.0  witness 1.0 }
  newtype BV = bv8 { invariant this != 7  witness 0 }
  method Reals(r: R) returns (s: R) {
    s := r * 2.0;
    s := r - 1.0; // error: the invariant might not hold
  }
  method Bits(b: BV) returns (c: BV) {
    c := b + 1; // error: the invariant might not hold
  }
  method BitsChecked(b: BV) returns (c: BV) requires b != 6 { c := b + 1; }
}
