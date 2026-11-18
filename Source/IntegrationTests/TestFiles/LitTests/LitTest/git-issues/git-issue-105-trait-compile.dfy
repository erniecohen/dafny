// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --target py "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of traits, compiled.  A trait
// with invariants, and its raw type, are the trait in the target.  A test against a class or a
// reference trait from a type that is not raw tests the dynamic type alone, since every object
// satisfies the invariants of its class, a general trait's value included; a test of a general trait's
// value against 'raw D' tests the constructor alone.

module M {
  trait Account extends object {
    const balance: int
    invariant balance >= 0
    function Describe(): string
  }
  class Savings extends Account {
    const rate: int
    invariant rate >= 1
    constructor (b: int, r: int) requires b >= 0 && r >= 1 { balance := b; rate := r; }
    function Describe(): string { "savings" }
  }
  class Plain extends Account {
    constructor (b: int) requires b >= 0 { balance := b; }
    function Describe(): string { "plain" }
  }
  trait Shape {
    const sides: int := 4
    invariant sides >= 3
    function Area(): int
  }
  datatype Square extends Shape = Square(s: int) {
    invariant s >= 0
    function Area(): int { s * s }
  }
  class Tile extends Shape {
    const side: int
    invariant side > 0
    constructor (n: int) requires n > 0 { side := n; }
    function Area(): int { side * side }
  }
  method Main() {
    var s := new Savings(10, 2);
    var p := new Plain(5);
    var accounts: seq<Account> := [s, p];
    for i := 0 to |accounts| {
      var a := accounts[i];
      print a.Describe(), " ", a.balance, " ", a is Savings, " ", a is Plain, "\n";
    }
    var o: object := s;
    print o is Account, " ", o is Savings, "\n";
    var q := Square(3);
    var shapes: seq<Shape> := [q];
    print shapes[0].Area(), " ", shapes[0].sides, " ", shapes[0] is raw Square, "\n";
    var r: raw Square := Square(-1);
    var rs: raw Shape := r;
    print rs is raw Square, "\n";
    var t := new Tile(2);
    var mixed: seq<Shape> := [q, t];
    for i := 0 to |mixed| {
      var sh := mixed[i];
      print sh.Area(), " ", sh is Tile, " ", sh is raw Tile, " ", sh is raw Square, "\n";
    }
  }
}
