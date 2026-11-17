// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), third
// stage: programs where a type of one member of a recursive module group extends a trait of
// another member, and that are accepted.  The declarations of a group mean what they would mean in
// one module, so each program below verifies, and each member named Bad is a control: a method or
// lemma that states something false, or a class whose overrides break the specification of the
// trait.  The errors of the verifier are exactly those for the Bad members.  The second run adds
// --modular-termination, where the members of a group count as one module.
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%s" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --modular-termination "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// two classes of two members whose overrides call each other through the trait, with a measure
module Tie1 {
  import Tie2
  import Tie3
  trait T {
    function A(other: T, n: nat): int
      decreases n
    function B(other: T, n: nat): int
      decreases n
  }
  function UseX(x: Tie2.X): int { 0 }
  function UseY(y: Tie3.Y): int { 0 }
}
module Tie2 {
  import Tie1
  class X extends Tie1.T {
    constructor () { }
    function A(other: Tie1.T, n: nat): int
      decreases n
    {
      0
    }
    function B(other: Tie1.T, n: nat): int
      decreases n
    {
      if n == 0 then 0 else other.A(this, n - 1)
    }
  }
}
module Tie3 {
  import Tie1
  class Y extends Tie1.T {
    constructor () { }
    function A(other: Tie1.T, n: nat): int
      decreases n
    {
      if n == 0 then 1 else other.B(this, n - 1) + 1
    }
    function B(other: Tie1.T, n: nat): int
      decreases n
    {
      1
    }
  }
}
module Tie4 {
  import Tie1
  import Tie2
  import Tie3
  method Good() {
    var x := new Tie2.X();
    var y := new Tie3.Y();
    assert x.B(y, 2) == y.A(x, 1) == x.B(y, 0) + 1 == 1;
  }
  method Bad()
    ensures false
  {
    var x := new Tie2.X();
    var y := new Tie3.Y();
    assert x.B(y, 2) == y.A(x, 1);
  }
}

// an expression tree: the trait and the evaluator are in one member, the nodes are datatypes of
// another, and the evaluation of a node calls back through the trait
module Expr1 {
  import Expr2
  trait Node {
    function Size(): nat
    function Eval(fuel: nat): int
      decreases fuel
  }
  function EvalAll(nodes: seq<Node>, fuel: nat): int
    decreases fuel, |nodes|
  {
    if nodes == [] || fuel == 0 then 0 else nodes[0].Eval(fuel - 1) + EvalAll(nodes[1..], fuel)
  }
  lemma Three()
    ensures Expr2.Sum([Expr2.Lit(1), Expr2.Lit(2)]).Eval(2) == 3
  {
    var one: Node := Expr2.Lit(1);
    var two: Node := Expr2.Lit(2);
    assert one is Expr2.Lit && two is Expr2.Lit;
    assert one.Eval(1) == 1 && two.Eval(1) == 2;
    assert [one, two][1..] == [two];
    assert [two][1..] == [];
    assert EvalAll([two], 2) == 2;
    assert EvalAll([one, two], 2) == 3;
  }
  lemma Bad()
    ensures Expr2.Sum([Expr2.Lit(1), Expr2.Lit(2)]).Eval(2) == 4
  {
    var one: Node := Expr2.Lit(1);
    var two: Node := Expr2.Lit(2);
    assert one is Expr2.Lit && two is Expr2.Lit;
    assert one.Eval(1) == 1 && two.Eval(1) == 2;
    assert [one, two][1..] == [two];
    assert [two][1..] == [];
    assert EvalAll([two], 2) == 2;
    assert EvalAll([one, two], 2) == 3;
  }
}
module Expr2 {
  import Expr1
  datatype Lit extends Expr1.Node = Lit(value: int) {
    function Size(): nat { 1 }
    function Eval(fuel: nat): int
      decreases fuel
    {
      value
    }
  }
  datatype Sum extends Expr1.Node = Sum(parts: seq<Expr1.Node>) {
    function Size(): nat { 1 + |parts| }
    function Eval(fuel: nat): int
      decreases fuel
    {
      Expr1.EvalAll(parts, fuel)
    }
  }
}

// a reference trait with state, a class of another member, and a method of the trait's member that
// creates the class
module State1 {
  import State2
  trait Counter extends object {
    var count: nat
    method Step()
      modifies this
      ensures count == old(count) + Stride()
    function Stride(): nat
      ensures Stride() > 0
  }
  method Twice(c: Counter)
    modifies c
    ensures c.count == old(c.count) + 2 * c.Stride()
  {
    c.Step();
    c.Step();
  }
  method Main() {
    var c := new State2.ByThree();
    Twice(c);
    assert c.count == 6;
  }
  method Bad() {
    var c := new State2.ByThree();
    Twice(c);
    assert c.count == 5;
  }
}
module State2 {
  import State1
  class ByThree extends State1.Counter {
    constructor ()
      ensures count == 0
    {
      count := 0;
    }
    method Step()
      modifies this
      ensures count == old(count) + Stride()
    {
      count := count + 3;
    }
    function Stride(): nat
      ensures Stride() > 0
    {
      3
    }
  }
}

// overrides are checked against the specification of the trait of the other member
module Override1 {
  import Override2
  trait T {
    function F(): nat
      ensures F() > 0
    method M(n: int) returns (r: int)
      requires n > 0
      ensures r > 0
    lemma L(n: int)
      ensures n * n >= 0
  }
  lemma Positive(t: T)
    ensures t.F() > 0
  {
  }
}
module Override2 {
  import Override1
  class Good extends Override1.T {
    function F(): nat
      ensures F() > 0
    {
      1
    }
    method M(n: int) returns (r: int)
      requires n > 0
      ensures r > 0
    {
      r := n;
    }
    lemma L(n: int)
      ensures n * n >= 0
    {
    }
  }
  class Bad extends Override1.T {
    function F(): nat  // no postcondition
    {
      0
    }
    method M(n: int) returns (r: int)  // a stronger precondition and a weaker postcondition
      requires n > 1
      ensures r >= 0
    {
      r := 0;
    }
    lemma L(n: int)  // a stronger precondition
      requires n > 0
      ensures n * n >= 0
    {
    }
  }
}

// a trait of one member that extends a trait of another, a class in a third: the class inherits
// the members of both, and the other members use them through any of the three types
module Chain1 {
  import Chain2
  import Chain3
  trait Shape {
    const sides: nat
    function Area(): nat
    function Doubled(): nat { 2 * Area() }
  }
  lemma Use(s: Chain3.Square)
    requires s.side == 3 && s.sides == 4
    ensures s.Doubled() == 18 && s.Perimeter() == 12
  {
    var r: Chain2.Regular := s;
    var sh: Shape := s;
    assert sh.Doubled() == r.Doubled() == 2 * s.Area();
  }
  lemma Bad(s: Chain3.Square)
    requires s.side == 3
    ensures s.Doubled() == 9
  {
  }
}
module Chain2 {
  import opened Chain1
  trait Regular extends Shape {
    const side: nat
    function Perimeter(): nat { sides * side }
  }
}
module Chain3 {
  import C2 = Chain2
  class Square extends C2.Regular {
    constructor (side: nat)
      ensures this.side == side && sides == 4
    {
      this.side := side;
      this.sides := 4;
    }
    function Area(): nat { side * side }
  }
  method Make() returns (s: Square)
    ensures s.Perimeter() == 8 && s.Doubled() == 8
  {
    s := new Square(2);
  }
  method Bad() returns (s: Square)
    ensures s.Perimeter() == 9
  {
    s := new Square(2);
  }
}

// a generic trait, type tests and casts between the trait and the types of other members
module Generic1 {
  import Generic2
  trait Box<X> {
    function Get(): X
  }
  function Unwrap(b: Box<int>): int {
    if b is Generic2.IntBox then (b as Generic2.IntBox).value else b.Get()
  }
  lemma Same(b: Box<int>)
    ensures Unwrap(b) == b.Get()
  {
  }
  lemma Bad(b: Box<int>)
    ensures Unwrap(b) == 0
  {
  }
}
module Generic2 {
  import Generic1
  datatype IntBox extends Generic1.Box<int> = IntBox(value: int) {
    function Get(): int { value }
  }
  datatype Pair<X> extends Generic1.Box<X> = Pair(first: X, second: X) {
    function Get(): X { first }
  }
  lemma Use()
    ensures Generic1.Unwrap(IntBox(7)) == 7 && Generic1.Unwrap(Pair(1, 2)) == 1
  {
    var b: Generic1.Box<int> := Pair(1, 2);
    assert b is Pair<int>;
  }
}

// reference traits and other traits, each way between two members, through an opened import
module Mixed1 {
  import opened Mixed2
  trait R1 extends object { }
  trait V1 { }
  class C1 extends R2 { }
  datatype D1 extends V2 = D1
  trait R3 extends R2 { }
  trait V3 extends V2 { }
  method M(c: C2?, d: D2) returns (r: R1?, v: V1) {
    r := c;
    v := d;
  }
  method Bad(r: R2) {
    assert r is C1;
  }
}
module Mixed2 {
  import M1 = Mixed1
  trait R2 extends object { }
  trait V2 { }
  class C2 extends M1.R1 { }
  datatype D2 extends M1.V1 = D2
  class C4 extends M1.R3 { }
  datatype D4 extends M1.V3 = D4
  method M(c: M1.C1?, d: M1.D1, c4: C4?) returns (r: R2?, v: V2, r3: M1.R3?) {
    r := c;
    v := d;
    r3 := c4;
    r := r3;
  }
}
