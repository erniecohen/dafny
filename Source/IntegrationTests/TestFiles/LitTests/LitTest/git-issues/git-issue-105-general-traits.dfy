// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of general traits and the
// datatypes that extend them: section 4.1 of docs/dev/issue105-member-invariants-plan.md.  A trait 'G'
// with invariants denotes the values of 'raw G' that satisfy them, and 'raw D' is a subtype of 'raw G'.
// A 'raw D' converts to 'G' as a 'D', with all of D's invariants checked, but through 'raw G' only G's
// are checked, so a 'G' may hold a value that is not a 'D'.  That is why a test of a 'G' against 'D'
// is ghost (git-issue-105-trait-errors.dfy), and a test against 'raw D' is not.

module Shapes {
  trait Shape {
    const sides: int := 4
    invariant sides >= 3
  }
  datatype Square extends Shape = Square(s: int) {
    invariant s >= 0
  }
  datatype Blob extends Shape = Blob(a: int)

  // as V3, for a general trait
  method V3(sh: Shape) {
    assert false;                                      // error
  }

  method Assumed(sh: Shape, q: Square, ss: seq<Shape>) {
    assert sh.sides >= 3;
    assert q.sides >= 3 && q.s >= 0;
    assert forall x <- ss :: x.sides >= 3;
  }
  method Construct() returns (q: Square, sh: Shape, b: Shape) {
    q := Square(3);
    sh := Square(3);
    b := Blob(-1);
  }
  method ConstructBad() returns (sh: Shape) {
    sh := Square(-1);                                  // error: Square's invariant
  }
  method RawToTrait(r: raw Square) returns (sh: Shape) {
    sh := r;                                           // error: Square's invariant (the inherited one holds)
  }
  method RawToRawTrait(r: raw Square) returns (x: raw Shape) {
    x := r;
  }
  method ThroughRawTrait(r: raw Square) returns (sh: Shape) {
    var x: raw Shape := r;
    sh := x;                                           // Shape's invariant only, which holds
  }
  lemma AShapeThatIsNotASquare() returns (sh: Shape)
    ensures sh is raw Square && !(sh is Square)
  {
    var x: raw Shape := Square(-1);
    sh := x;
    assert (sh as raw Square).s == -1;
  }
  method Test(sh: Shape) returns (b: bool) {
    b := sh is raw Square;
  }
}

// Two values whose least common supertype is a trait with invariants join at that trait if neither is
// raw, and at its raw type otherwise
module Joins {
  trait G {
    const k: int
    invariant k > 0
  }
  datatype A extends G = A(x: int)
  datatype B extends G = B(y: int) {
    invariant y > 0
  }
  method Join(a: A, b: B, c: bool) returns (h: G) {
    var g := if c then a else b;                       // a G
    h := g;
  }
  method RawJoin(a: raw A, b: B, c: bool) returns (h: G) {
    var g := if c then a else b;                       // a raw G
    h := g;                                            // error: G's invariant
  }
  method RawJoinChecked(b: raw B, a: A, c: bool) returns (h: G) requires b.k > 0 {
    var g := if c then b else a;                       // a raw G, which G's invariant alone makes a G
    h := g;
  }
  method Seqs(a: A, b: B) returns (t: seq<G>) {
    var s := [a, b];                                   // a seq<G>
    t := s;
  }
}

// A generic trait's invariants are instantiated at the parent type arguments
module Generic {
  trait G<X> {
    const k: int
    invariant k != 0
  }
  datatype Box<Y> extends G<Y> = Box(y: Y, n: int) {
    invariant n > 0
  }
  method Finished(b: Box<int>) returns (g: G<int>) {
    g := b;
    assert g.k != 0 && b.n > 0;
  }
  method Raw(b: raw Box<int>) returns (g: G<int>) {
    g := b;                                            // error: each of Box's two invariants
  }
}

// A datatype that only inherits invariants may declare a witness, which must satisfy them, as well as
// its own
module Witnesses {
  trait G {
    const k: int
    invariant k > 0
  }
  datatype W extends G = W(x: int) {
    witness W(0)                                       // error: G's invariant
  }
  trait H {
    const k: int := 1
    invariant k > 0
  }
  datatype V extends H = V(x: int) {
    witness V(0)
  }
  datatype U extends H = U(x: int) {
    invariant x > 0
    ghost witness U(0)                                 // error: U's invariant
  }
  method Need<X(0)>() { }
  method Use() {
    Need<V>();
  }
}

// A class may extend a general trait with invariants.  Its objects satisfy all its invariants once they
// are constructed, so a test of a 'G' against the class compiles, as for a reference trait.
module ClassOfGeneral {
  trait G {
    const k: int
    invariant k > 0
  }
  class C extends G {
    const m: int
    invariant m > k
    constructor (k: int) requires k > 0 { this.k := k; m := k + 1; }
  }
  class Bad extends G {
    constructor () { k := 0; }                         // error: G's invariant, at the implicit 'new;'
  }
  method M(c: C, r: raw C) returns (g: G, x: raw G) {
    g := c;
    assert g.k > 0;
    x := r;
    g := r;                                            // error: each of C's two invariants
  }
  method Test(g: G) returns (b: bool, b': bool) {
    b := g is C;
    b' := g is raw C;
  }
}
