// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%S/Inputs/git-issue-105-new-by.dfy" >> "%t"
// RUN: %verify --member-invariants --bprint:"%t.bpl" "%S/Inputs/git-issue-105-requires-axiom.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %OutputCheck --file-to-check "%t.bpl" "%s"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): attempts to prove false through
// newtypes with invariants (their operators, a newtype over one, native types and the sugar of the
// header), through a constructor's 'new' and 'new by', and through raw values that fail an earlier
// invariant.  Each attempt ensures false, and each is rejected, at the attempt or, where a header's
// witness fails, at the type, as for a subset type whose witness fails.  The positive controls show
// that the invariants hold of the values of each type.

module Operators {
  newtype NZ = bv8 {
    invariant this != 0
    witness 1
  }
  lemma Holds(a: NZ) ensures a as bv8 != 0 { }
  lemma Xor(a: NZ) ensures false {
    var z: NZ := a ^ a;                                // error
  }
  lemma Not(a: NZ) ensures false {
    var z: NZ := !a;                                   // error: a may be 0xFF
  }
  lemma Shift(a: NZ) ensures false {
    var z: NZ := a << 8;                               // error
  }
  lemma Rotate(a: NZ) ensures false {
    // RotateLeft gives the base type, so only a conversion makes its result an NZ
    var z: NZ := (a as bv8 ^ a as bv8).RotateLeft(1) as NZ;   // error
  }
  // A newtype over a newtype with invariants has both types' invariants
  newtype NZ2 = NZ witness 2 {
    invariant this != 1
  }
  lemma Both(b: NZ2) ensures b as bv8 != 0 && b as bv8 != 1 { }
  lemma Lift(a: NZ) ensures false {
    var b: NZ2 := a as NZ2;                            // error: a may be 1
  }
  newtype Ev = int {
    invariant this % 2 == 0
    witness 0
  }
  lemma ForLoop() ensures false {
    for i: Ev := 0 to 4 { }                            // error: 1 is not an Ev
  }
  newtype Pos = real {
    invariant this > 0.0
    witness 1.0
  }
  lemma Add(a: Pos) ensures a as real + a as real > 0.0 {
    var b: Pos := a + a;
  }
  lemma Sub(a: Pos) ensures false {
    var z: Pos := a - a;                               // error
  }
  newtype NotA = char {
    invariant this != 'a'
    witness 'b'
  }
  lemma CharConversion() ensures false {
    var c: NotA := 'a' as NotA;                        // error
  }
}

module Native {
  newtype Small = int {
    invariant 0 <= this < 100
    witness 0
  }
  method Double(a: Small) returns (c: Small) ensures false {
    c := a + a;                                        // error
  }
  method Negate(a: Small) returns (c: Small) ensures false {
    c := -a;                                           // error
  }
  newtype {:nativeType "byte"} Byte = x: int | 0 <= x < 256 {
    invariant this != 0
    witness 1
  }
  method Keep(a: Byte) returns (c: Byte) ensures c as int != 0 {
    c := a;
  }
  method Diff(a: Byte) returns (c: Byte) ensures false {
    c := a - a;                                        // error
  }
}

module Header {
  // A witness clause in the header is a witness of the invariants too
  newtype W = x: int | x > 0 witness 3 {               // error
    invariant false
  }
  lemma Witness() ensures false { var w: W :| true; }  // as for a subset type whose witness fails
  // Without a witness clause, the old syntax's default 0 must satisfy the invariants
  newtype Z = x: int | true {                          // error
    invariant this != 0
  }
  lemma Default() ensures false { var z: Z :| z as int == 0; }   // error
  newtype O = x: int | true witness * {
    invariant false
  }
  lemma OptOut() ensures false { var o: O :| true; }   // error: O may be empty
  newtype G = x: int | x > 0 ghost witness 2 {         // error
    invariant this > 5
  }
  lemma GhostWitness() ensures false { var g: G :| g as int < 5; }   // error
}

// A constructor checks the invariants at 'new;', or at its end without one.  'new by' is not yet
// part of the language (Inputs/git-issue-105-new-by.dfy).
module Constructors {
  class C {
    const x: int
    invariant x > 0
    constructor One() ensures x > 0 {
      x := 1;
      new;
    }
    constructor Zero() ensures false {
      x := 0;
      new;                                             // error
    }
    constructor Implicit() ensures false {
      x := 0;
    }                                                  // error
  }
}

// A raw value that fails an earlier invariant gives no #canCall of a later invariant's callees: the
// precondition of each invariant is the invariants before it (the Boogie text is checked below, on
// Inputs/git-issue-105-requires-axiom.dfy)
module Raw {
  function H(n: int): bool requires n > 0 ensures n > 0 { true }
  datatype D = D(n: int) {
    invariant n > 0
    invariant H(n)
  }
  lemma Value() ensures false {                        // error
    var r: raw D := D(-1);
    var b := r is D;
  }
  lemma Parameter(r: raw D) requires r.n == -1 ensures false {   // error
    var b := r is D;
  }
}

// CHECK: _module.D.invariant_h0#requires\(this\) == true
// CHECK: _module.D.invariant_h1#requires\(this\) == _module.D.invariant_h0\(this\)
// CHECK: ==> _module.D.invariant_h2#requires\(this\)
// CHECK-NEXT: == \(_module.D.invariant_h0\(this\) && _module.D.invariant_h1\(this\)\)\);
