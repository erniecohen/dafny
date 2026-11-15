// The issue's program, with "decreases this" on A.top: a member of a trait that is not a reference
// type gets no "this" in its default decreases clause, so A.top's default is empty, which is TOP.
// The cast "this as A" is written as an assignment, which both resolvers accept.
module Ma {
  trait {:termination false} A {
    ghost predicate has(d: int, b: bool) decreases this
    ghost predicate p(d: int) { forall b :: has(d, b) }
    function top(): (d: int) decreases this ensures p(d)
  }

  datatype C extends A = C(a: A) {
    ghost predicate has(d: int, b: bool) decreases this { a.has(d, b) }
    function top(): (r: int) decreases this ensures p(r) {
      var ad: A := this;
      var r := a.top();
      assert p(r) by {
        forall b ensures ad.has(r, b) {
          assert has(r, b);
        }
      }
      r
    }
  }
}

// Structural descent through two datatypes that extend the same trait.
module TwoDatatypes {
  trait T {
    function size(): nat decreases this
  }

  datatype L extends T = Nil | Cons(next: T) {
    function size(): nat decreases this {
      match this
      case Nil => 0
      case Cons(next) => 1 + next.size()
    }
  }

  datatype N extends T = N(left: T, right: T) {
    function size(): nat decreases this {
      1 + left.size() + right.size()
    }
  }

  lemma SizeOfCons(t: T)
    ensures Cons(t).size() == 1 + t.size()
  {
  }
}

// A method, and a lemma, with "decreases this" in a datatype, calling through a trait-typed field.
module Methods {
  trait T {
    method Walk() returns (n: nat) decreases this
    lemma Visit() decreases this
  }

  datatype D extends T = Leaf | Node(child: T, sibling: T) {
    method Walk() returns (n: nat) decreases this {
      match this
      case Leaf => n := 0;
      case Node(child, sibling) =>
        var a := child.Walk();
        var b := sibling.Walk();
        n := a + b + 1;
    }
    lemma Visit() decreases this {
      if Node? {
        child.Visit();
        sibling.Visit();
      }
    }
  }
}

// A datatype component and a trait component in one decreases tuple.
module Tuples {
  trait T {
    function f(n: nat): nat decreases this, n
  }

  datatype D extends T = Leaf | Node(child: T) {
    function f(n: nat): nat decreases this, n {
      match this
      case Leaf => if n == 0 then 0 else f(n - 1)
      case Node(child) => child.f(n + 10)
    }
  }
}
